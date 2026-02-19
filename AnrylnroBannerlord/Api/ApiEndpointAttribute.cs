using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AnrylnroBannerlord.Api
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class ApiEndpointAttribute : Attribute
    {
        public ApiEndpointAttribute(string method, string path)
        {
            Method = method;
            Path = path;
        }

        public string Method { get; }
        public string Path { get; }
        public bool RequireApiKey { get; set; }
        public bool HostOnly { get; set; }
        public bool ChildOnly { get; set; }
    }

    internal delegate Task ApiEndpointHandler(HttpContext context, CancellationToken token);
}
