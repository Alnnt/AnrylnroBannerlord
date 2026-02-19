using AnrylnroBannerlord.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Sockets;
using TaleWorlds.MountAndBlade;

namespace AnrylnroBannerlord.Api
{
    public partial class ApiServer
    {
        private void StartInternal(string sharedApiPort)
        {
            _sharedApiPort = sharedApiPort;
            _gamePort = Module.CurrentModule.StartupInfo.ServerPort;
            _cts = new CancellationTokenSource();
            ModLogger.Log($"API starting. sharedApiPort={_sharedApiPort}, gamePort={_gamePort}.");

            // 先尝试成为 Host；失败后再判断端口是否被同插件占用。
            if (TryStartHost(sharedApiPort))
            {
                _isHost = true;
                _running = true;
                _ = RegisterLifecycleAsync(RegisterUrl);
                ModLogger.Log($"HTTP API started as host on port {_sharedApiPort}, game port {_gamePort}.");
                return;
            }

            if (!IsPluginServerOnPort(sharedApiPort))
            {
                ModLogger.Error($"HTTP start failed: port {sharedApiPort} is occupied by another process (not Anrylnro API).");
                return;
            }

            _isHost = false;
            _localChildPort = FindAvailablePort(17000);
            ModLogger.Log($"Host exists, switch to child mode. localChildPort={_localChildPort}, gamePort={_gamePort}.");

            if (!TryStartChild(_localChildPort))
            {
                ModLogger.Error($"HTTP child mode failed: cannot start local child listener. (failed to listen http://127.0.0.1:{_localChildPort}/)");
                return;
            }

            _running = true;
            _ = RegisterLifecycleAsync(RegisterUrl);
            _ = Task.Run(() => ChildHeartbeatLoopAsync(_cts.Token));
            ModLogger.Log($"HTTP API started as child. Host port {_sharedApiPort}, local port {_localChildPort}, game port {_gamePort}.");
        }

        private void StopInternal()
        {
            if (!_running)
            {
                return;
            }

            _running = false;

            try
            {
                _cts?.Cancel();
            }
            catch
            {
                ModLogger.Error("Failed to cancel API server tasks.");
            }

            RegisterLifecycleSync(UnregisterUrl);

            try
            {
                if (_webHost != null)
                {
                    _webHost.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    _webHost.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                _webHost = null;
            }

            try
            {
                _httpClient.Dispose();
            }
            catch
            {
            }

            ModLogger.Log("HTTP API stopped.");
        }

        private bool TryStartHost(string port)
        {
            return TryStartServer($"http://0.0.0.0:{port}");
        }

        private bool TryStartChild(string port)
        {
            return TryStartServer($"http://127.0.0.1:{port}");
        }

        private bool TryStartServer(string url)
        {
            try
            {
                IHost host = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel();
                        webBuilder.UseUrls(url);
                        webBuilder.Configure(app =>
                        {
                            app.Run(async context =>
                            {
                                CancellationToken token = _cts == null
                                    ? context.RequestAborted
                                    : CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, context.RequestAborted).Token;
                                await HandleRequestAsync(context, token);
                            });
                        });
                    })
                    .Build();

                host.Start();
                _webHost = host;
                ModLogger.Log("Kestrel started on " + url);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    _webHost?.Dispose();
                }
                catch
                {
                }

                _webHost = null;
                string reason = ClassifyBindFailure(ex);
                ModLogger.Warn($"Failed to start Kestrel on {url}: {reason}. RawError={ex.Message}");
                return false;
            }
        }

        private static string ClassifyBindFailure(Exception ex)
        {
            if (ContainsException<AddressInUseException>(ex) || ContainsSocketError(ex, SocketError.AddressAlreadyInUse))
            {
                return "port already in use";
            }

            if (ContainsSocketError(ex, SocketError.AccessDenied))
            {
                return "permission denied (insufficient privilege)";
            }

            if (ContainsSocketError(ex, SocketError.AddressNotAvailable))
            {
                return "address not available or invalid bind address";
            }

            return "unknown bind/startup error";
        }

        private static bool ContainsSocketError(Exception ex, SocketError target)
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is SocketException socketEx && socketEx.SocketErrorCode == target)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static bool ContainsException<TException>(Exception ex) where TException : Exception
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is TException)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private async Task HandleRequestAsync(HttpContext context, CancellationToken token)
        {
            try
            {
                context.Response.Headers[PluginMarkerHeader] = PluginMarkerValue;

                // 1) 找到端点
                // 2) 校验 Host/Child 模式约束
                // 3) 校验 API Key（如果该端点需要）
                // 4) 执行业务处理器
                string path = context.Request.Path.Value ?? string.Empty;
                string key = BuildEndpointKey(context.Request.Method, path);

                if (!_endpointHandlers.TryGetValue(key, out ApiEndpointHandler handler) || !_endpointMeta.TryGetValue(key, out ApiEndpointAttribute meta))
                {
                    context.Response.StatusCode = 404;
                    ModLogger.Warn($"HTTP 404: endpoint not found ({context.Request.Method} {path}).");
                    return;
                }

                if (meta.HostOnly && !_isHost)
                {
                    context.Response.StatusCode = 404;
                    ModLogger.Warn($"HTTP 404: host-only endpoint requested on child ({context.Request.Method} {path}).");
                    return;
                }

                if (meta.ChildOnly && _isHost)
                {
                    context.Response.StatusCode = 404;
                    ModLogger.Warn($"HTTP 404: child-only endpoint requested on host ({context.Request.Method} {path}).");
                    return;
                }

                if (meta.RequireApiKey && !CheckApiKey(context))
                {
                    return;
                }

                await handler(context, token);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                ModLogger.Warn("HTTP request failed: " + ex.Message);
            }
        }

        private bool CheckApiKey(HttpContext context)
        {
            if (string.Equals(context.Request.Headers[ApiKeyHeader], _apiKey, StringComparison.Ordinal))
            {
                return true;
            }

            context.Response.StatusCode = 403;
            ModLogger.Warn($"HTTP 403: invalid API key from {context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}.");
            return false;
        }
    }
}
