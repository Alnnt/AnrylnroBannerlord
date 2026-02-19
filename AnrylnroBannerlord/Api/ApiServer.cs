using AnrylnroBannerlord.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;

namespace AnrylnroBannerlord.Api
{
    public partial class ApiServer
    {
        private const string PluginMarkerHeader = "X-Anrylnro-ApiServer";
        private const string PluginMarkerValue = "1";
        private const string RegisterUrl = "http://anrylnro.wm-valley.com/bannerlord/register";
        private const string UnregisterUrl = "http://anrylnro.wm-valley.com/bannerlord/unregister";
        private const string ApiKeyHeader = "X-API-KEY";

        // 共享端口上的 API 密钥（仅用于受保护端点）
        private readonly string _apiKey = ConfigManager.GetConfig("AnrylnroApiKey", "YourSecretKey");
        private readonly HttpClient _httpClient = new HttpClient();

        // Host 维护的子节点路由表：gamePort -> childBaseUrl
        private readonly ConcurrentDictionary<int, string> _children = new ConcurrentDictionary<int, string>();

        // 注解端点注册表：METHOD + PATH -> 处理器/元数据
        private readonly Dictionary<string, ApiEndpointHandler> _endpointHandlers = new Dictionary<string, ApiEndpointHandler>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ApiEndpointAttribute> _endpointMeta = new Dictionary<string, ApiEndpointAttribute>(StringComparer.OrdinalIgnoreCase);

        private IHost _webHost;
        private CancellationTokenSource _cts;

        // _sharedApiPort: 所有实例竞争的共享端口（Host 对外监听）
        // _localChildPort: Child 本地监听端口（仅 127.0.0.1）
        // _gamePort: 当前游戏服端口（用于路由区分）
        private string _sharedApiPort;
        private string _localChildPort;
        private int _gamePort;

        private bool _isHost;
        private bool _running;

        public static ApiServer Instance { get; private set; }

        public ApiServer()
        {
            RegisterEndpoints();
        }

        public static void Start()
        {
            if (Instance != null)
            {
                return;
            }

            string port = ConfigManager.GetConfig("AnrylnroPort");
            if (string.IsNullOrEmpty(port))
            {
                ModLogger.Warn("API port not configured, using default 7011.");
                ModLogger.Warn("To configure, add 'AnrylnroPort {your_port}' to the config file.");
                ModLogger.Warn("Different sub servers on the same server can use the same port.");
                port = "7011";
            }

            Instance = new ApiServer();
            Instance.StartInternal(port);
        }

        public static void Stop()
        {
            Instance?.StopInternal();
            Instance = null;
        }

        private static string BuildEndpointKey(string method, string path)
        {
            return method + " " + path;
        }
    }
}
