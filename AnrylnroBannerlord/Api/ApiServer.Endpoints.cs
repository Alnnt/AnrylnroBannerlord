using AnrylnroBannerlord.Network;
using AnrylnroBannerlord.Utils;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AnrylnroBannerlord.Api
{
    public partial class ApiServer
    {
        [ApiEndpoint("GET", "/ping")]
        private Task PingAsync(HttpContext context, CancellationToken token)
        {
            return HandlePingAsync(context, token);
        }

        private async Task HandlePingAsync(HttpContext context, CancellationToken token)
        {
            bool handledByForward = await TryForwardGetToChildAsync(
                context,
                token,
                endpointPath: "/ping",
                requireApiKey: false,
                endpointName: "Ping",
                (content, ct) => WritePlainTextAsync(context, content, ct));
            if (handledByForward)
            {
                return;
            }

            await WritePlainTextAsync(context, "OK", token);
        }

        [ApiEndpoint("POST", "/_anrylnro/children/register", HostOnly = true)]
        private Task RegisterChildAsync(HttpContext context, CancellationToken token)
        {
            string gamePortText = context.Request.Query["gamePort"];
            string childPort = context.Request.Query["childPort"];

            if (!int.TryParse(gamePortText, out int childGamePort) || string.IsNullOrWhiteSpace(childPort))
            {
                context.Response.StatusCode = 400;
                ModLogger.Warn($"Child register rejected: invalid args. gamePort={gamePortText}, childPort={childPort}.");
                return Task.CompletedTask;
            }

            _children[childGamePort] = $"http://127.0.0.1:{childPort}";
            ModLogger.Log($"Child Alive. gamePort={childGamePort}, childPort={childPort}.");
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        [ApiEndpoint("GET", "/players")]
        private Task GetPlayersAsync(HttpContext context, CancellationToken token)
        {
            return HandlePlayersAsync(context, token);
        }

        private async Task HandlePlayersAsync(HttpContext context, CancellationToken token)
        {
            bool handledByForward = await TryForwardGetToChildAsync(
                context,
                token,
                endpointPath: "/players",
                requireApiKey: true,
                endpointName: "Players",
                (content, ct) => WriteJsonAsync(context, content, ct));
            if (handledByForward)
            {
                return;
            }

            if (!CheckApiKey(context))
            {
                return;
            }

            await RefreshPlayersAsync(token);
            List<PlayerSnapshot> data = PlayerDataStore.GetSnapshot();
            ModLogger.Log($"Return local players snapshot. gamePort={_gamePort}, count={data.Count}.");
            string localJson = SimpleJson.SerializePlayers(data);
            await WriteJsonAsync(context, localJson, token);
        }

        private async Task RefreshPlayersAsync(CancellationToken token)
        {
            PlayerDataStore.RequestRefresh();
            while (PlayerDataStore.RefreshRequested && !token.IsCancellationRequested)
            {
                await Task.Delay(50, token);
            }
        }

        private async Task WriteJsonAsync(HttpContext context, string json, CancellationToken token)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = buffer.Length;
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length, token);
        }

        private async Task WritePlainTextAsync(HttpContext context, string text, CancellationToken token)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength = buffer.Length;
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length, token);
        }

        private async Task<bool> TryForwardGetToChildAsync(
            HttpContext context,
            CancellationToken token,
            string endpointPath,
            bool requireApiKey,
            string endpointName,
            Func<string, CancellationToken, Task> writeSuccessResponseAsync)
        {
            string targetGamePortText = context.Request.Query["gamePort"];
            bool hasTarget = int.TryParse(targetGamePortText, out int targetGamePort);
            bool shouldForward = _isHost && hasTarget && targetGamePort != _gamePort;
            if (!shouldForward)
            {
                return false;
            }

            if (!_children.TryGetValue(targetGamePort, out string childBaseUrl))
            {
                context.Response.StatusCode = 404;
                ModLogger.Warn($"{endpointName} forward failed: child not found for gamePort={targetGamePort}.");
                return true;
            }

            string url = childBaseUrl + endpointPath;
            (int StatusCode, string Content) forwardResult;
            if (requireApiKey)
            {
                string requestApiKey = context.Request.Headers[ApiKeyHeader];
                if (string.IsNullOrWhiteSpace(requestApiKey))
                {
                    context.Response.StatusCode = 403;
                    ModLogger.Warn($"{endpointName} forward rejected: missing API key in request.");
                    return true;
                }

                forwardResult = await GetStringWithApiKeyAsync(url, token, requestApiKey);
            }
            else
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url, token);
                string content = await response.Content.ReadAsStringAsync();
                forwardResult = ((int)response.StatusCode, content);
            }

            ModLogger.Log($"Forward {endpointPath} to child. targetGamePort={targetGamePort}, url={url}.");
            if (forwardResult.StatusCode < 200 || forwardResult.StatusCode >= 300)
            {
                context.Response.StatusCode = forwardResult.StatusCode;
                ModLogger.Warn($"{endpointName} forward failed: child returned HTTP {forwardResult.StatusCode}.");
                return true;
            }

            await writeSuccessResponseAsync(forwardResult.Content, token);
            return true;
        }

        private void RegisterEndpoints()
        {
            System.Reflection.MethodInfo[] methods = GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            int registeredCount = 0;
            foreach (System.Reflection.MethodInfo method in methods)
            {
                ApiEndpointAttribute endpoint = (ApiEndpointAttribute)Attribute.GetCustomAttribute(method, typeof(ApiEndpointAttribute));
                if (endpoint == null)
                {
                    continue;
                }

                ApiEndpointHandler handler = (ApiEndpointHandler)Delegate.CreateDelegate(typeof(ApiEndpointHandler), this, method, throwOnBindFailure: false);
                if (handler == null)
                {
                    ModLogger.Error($"Invalid endpoint signature: {method.Name}");
                    continue;
                }

                string key = BuildEndpointKey(endpoint.Method, endpoint.Path);
                if (_endpointHandlers.ContainsKey(key))
                {
                    ModLogger.Error($"Duplicate endpoint mapping: {endpoint.Method} {endpoint.Path}");
                    continue;
                }

                _endpointHandlers[key] = handler;
                _endpointMeta[key] = endpoint;
                registeredCount++;
            }

            ModLogger.Log($"Endpoint registration completed. total={registeredCount}.");
        }
    }
}
