using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    static class MContext
    {
        public static async Task WriteResponseAsync(this HttpContext context, string str,
            int status = StatusCodes.Status200OK, string type = "text/plain", HeaderDictionary headers = null)
        {
            if (headers != null)
                foreach (var (k, v) in headers)
                    context.Response.Headers.Add(k, v);
            if (!context.Response.Headers.Keys.Contains("X-Powered-By"))
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
            context.Response.ContentType = type;
            context.Response.StatusCode = status;
            await context.Response.WriteAsync(str);
        }

        public static async Task WriteResponseAsync(this HttpContext context, byte[] bytes,
            int status = StatusCodes.Status200OK, string type = "text/plain")
        {
            if (!context.Response.Headers.Keys.Contains("X-Powered-By"))
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
            context.Response.ContentType = type;
            context.Response.StatusCode = status;
            await context.Response.Body.WriteAsync(bytes);
        }
    }
}
