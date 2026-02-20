using AnrylnroBannerlord.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace AnrylnroBannerlord.Api
{
    public partial class ApiServer
    {
        private async Task ChildHeartbeatLoopAsync(CancellationToken token)
        {
            // Child 心跳策略：
            // 1) 周期向 Host 注册自身路由。
            // 2) 连续失败时尝试抢占共享端口晋升 Host。
            // 同机环境下共享端口天然互斥，理论上最终只有一个节点晋升成功。
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (await TryRegisterWithHostAsync(token))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token);
                        continue;
                    }

                    ModLogger.Warn($"Heartbeat register failed, start host election. gamePort={_gamePort}, sharedApiPort={_sharedApiPort}.");
                    if (TryPromoteToHost())
                    {
                        _isHost = true;
                        _children.Clear();
                        ModLogger.Log($"Host server switched to this instance on port {_sharedApiPort}.");
                        await RegisterLifecycleAsync(RegisterUrl);
                        return;
                    }

                    if (!IsPluginServerOnPort(_sharedApiPort))
                    {
                        ModLogger.Warn($"Host election failed: port {_sharedApiPort} is now occupied by non-plugin process.");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("Child heartbeat error: " + ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }

        private bool TryPromoteToHost()
        {
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

            ModLogger.Log($"Try promote to host on shared port {_sharedApiPort}.");
            bool promoted = TryStartHost(_sharedApiPort);
            ModLogger.Log(promoted
                ? $"Promote to host succeeded on port {_sharedApiPort}."
                : $"Promote to host failed on port {_sharedApiPort}.");
            return promoted;
        }

        private async Task<bool> TryRegisterWithHostAsync(CancellationToken token)
        {
            string url = $"http://127.0.0.1:{_sharedApiPort}/_anrylnro/children/register?gamePort={_gamePort}&childPort={_localChildPort}";
            using System.Net.Http.HttpRequestMessage request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);

            try
            {
                using System.Net.Http.HttpResponseMessage response = await _httpClient.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    ModLogger.Warn($"Heartbeat register rejected by host. HTTP {(int)response.StatusCode}.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Heartbeat register failed: {ex.Message}");
                return false;
            }
        }

        private bool IsPluginServerOnPort(string port)
        {
            try
            {
                HttpResponseMessage response = _httpClient.GetAsync($"http://127.0.0.1:{port}/ping").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    ModLogger.Warn($"Ping on port {port} returned HTTP {(int)response.StatusCode}.");
                    return false;
                }

                if (!response.Headers.TryGetValues(PluginMarkerHeader, out IEnumerable<string> values))
                {
                    ModLogger.Warn($"Ping on port {port} missing plugin marker header.");
                    return false;
                }

                foreach (string value in values)
                {
                    if (value == PluginMarkerValue)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Ping on port {port} failed: {ex.Message}");
                return false;
            }
        }

        private string FindAvailablePort(int start)
        {
            for (int port = start; port < start + 1000; port++)
            {
                if (TryUsePortOnce(port))
                {
                    return port.ToString();
                }
            }

            throw new InvalidOperationException("No available local port for child API server.");
        }

        private bool TryUsePortOnce(int port)
        {
            TcpListener probe = null;

            try
            {
                probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    probe?.Stop();
                }
                catch
                {
                }
            }
        }
    }
}

