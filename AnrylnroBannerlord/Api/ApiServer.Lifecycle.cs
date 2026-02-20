using AnrylnroBannerlord.Utils;
using System;

namespace AnrylnroBannerlord.Api
{
    public partial class ApiServer
    {
        public async Task RegisterLifecycleAsync(string url)
        {
            try
            {
                // 生命周期上报用于外部服列表：注册/注销都复用该方法。
                string publicIp = await GetPublicIpAsync();
                if (string.IsNullOrWhiteSpace(publicIp))
                {
                    ModLogger.Error("Lifecycle register skipped: cannot resolve public IP.");
                    return;
                }
                ModLogger.Log($"Your public IP: {publicIp}");


                string httpPort = _isHost ? _sharedApiPort : _localChildPort;
                string requestUrl = $"{url}?publicIp={Uri.EscapeDataString(publicIp)}&gamePort={_gamePort}&httpPort={httpPort}";
                ModLogger.Log($"Lifecycle call -> {url}, gamePort={_gamePort}, httpPort={httpPort}.");

                using System.Net.Http.HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ModLogger.Error($"Lifecycle call failed ({url}): HTTP {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Lifecycle call failed ({url}): {ex.Message}");
            }
        }

        private void RegisterLifecycleSync(string url)
        {
            try
            {
                RegisterLifecycleAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Lifecycle call failed ({url}): {ex.Message}");
            }
        }

        private async Task<(int StatusCode, string Content)> GetStringWithApiKeyAsync(string url, CancellationToken token, string apiKey)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(ApiKeyHeader, apiKey);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, token);
            string content = await response.Content.ReadAsStringAsync();
            return ((int)response.StatusCode, content);
        }

        private async Task<string> GetPublicIpAsync()
        {
            // 公网 IP 获取是“尽力而为”策略：主方案失败则依次回退。
            try
            {
                ModLogger.Log("Resolving public IP from ipinfo.io...");
                string ip = await _httpClient.GetStringAsync("https://ipinfo.io/ip");
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    return ip.Trim();
                }
            }
            catch
            {
                ModLogger.Warn("Failed to get public IP from ipinfo.io, trying alternatives...");
            }

            try
            {
                ModLogger.Log("Resolving public IP from api.ipify.org...");
                string ip = await _httpClient.GetStringAsync("https://api.ipify.org");
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    return ip.Trim();
                }
            }
            catch
            {
                ModLogger.Warn("Failed to get public IP from api.ipify.org, trying alternatives...");
            }

            try
            {
                string response = await _httpClient.GetStringAsync("https://myip.ipip.net");
                const string prefix = "当前 IP：";
                int startIndex = response.IndexOf(prefix, StringComparison.Ordinal);
                if (startIndex >= 0)
                {
                    startIndex += prefix.Length;
                    int endIndex = response.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, startIndex);
                    if (endIndex < 0)
                    {
                        endIndex = response.Length;
                    }

                    string ip = response.Substring(startIndex, endIndex - startIndex);
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        return ip.Trim();
                    }
                }
            }
            catch
            {
                ModLogger.Warn("Failed to get public IP from myip.ipip.net.");
            }
            ModLogger.Error("All methods to resolve public IP have failed.");
            return string.Empty;
        }
    }
}
