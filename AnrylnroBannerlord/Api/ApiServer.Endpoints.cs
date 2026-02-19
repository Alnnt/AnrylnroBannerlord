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
        [ApiEndpoint("GET", "/_anrylnro/ping")]
        private Task PingAsync(HttpContext context, CancellationToken token)
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
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
            ModLogger.Log($"Child registered. gamePort={childGamePort}, childPort={childPort}.");
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
            string targetGamePortText = context.Request.Query["gamePort"];
            bool hasTarget = int.TryParse(targetGamePortText, out int targetGamePort);
            bool shouldForward = _isHost && hasTarget && targetGamePort != _gamePort;

            if (shouldForward)
            {
                if (!_children.TryGetValue(targetGamePort, out string childBaseUrl))
                {
                    context.Response.StatusCode = 404;
                    ModLogger.Warn($"Players forward failed: child not found for gamePort={targetGamePort}.");
                    return;
                }

                string requestApiKey = context.Request.Headers[ApiKeyHeader];
                if (string.IsNullOrWhiteSpace(requestApiKey))
                {
                    context.Response.StatusCode = 403;
                    ModLogger.Warn("Players forward rejected: missing API key in request.");
                    return;
                }

                string url = childBaseUrl + "/players";
                ModLogger.Log($"Forward /players to child. targetGamePort={targetGamePort}, url={url}.");
                (int StatusCode, string Content) forwardResult = await GetStringWithApiKeyAsync(url, token, requestApiKey);
                if (forwardResult.StatusCode < 200 || forwardResult.StatusCode >= 300)
                {
                    context.Response.StatusCode = forwardResult.StatusCode;
                    ModLogger.Warn($"Players forward failed: child returned HTTP {forwardResult.StatusCode}.");
                    return;
                }

                await WriteJsonAsync(context, forwardResult.Content, token);
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
