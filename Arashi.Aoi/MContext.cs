using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    static class MContext
    {
        public static async Task WriteResponseAsync(this HttpContext context, string str,
            int status = StatusCodes.Status200OK, string type = "text/plain", HeaderDictionary headers = null)
        {
            try
            {
                if (headers != null)
                    foreach (var (k, v) in headers)
                        context.Response.Headers.Add(k, v);
                if (!context.Response.Headers.Keys.Contains("X-Powered-By"))
                    context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/Aoi");
                context.Response.ContentType = type;
                context.Response.StatusCode = status;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await context.Response.WriteAsync(str);
        }

        public static async Task WriteResponseAsync(this HttpContext context, byte[] bytes,
            int status = StatusCodes.Status200OK, string type = "text/plain")
        {
            try
            {
                if (!context.Response.Headers.Keys.Contains("X-Powered-By"))
                    context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/Aoi");
                context.Response.ContentType = type;
                context.Response.StatusCode = status;
                context.Response.Headers["Content-Length"] = bytes.Length.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await context.Response.Body.WriteAsync(bytes);
        }
    }
}
