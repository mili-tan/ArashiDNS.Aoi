using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using Arashi.Kestrel;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static Arashi.AoiConfig;

namespace Arashi.Aoi.Routes
{
    class AdminRoutes
    {
        public static void AdminRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.AdminPerfix + "/cache/ls", async context =>
            {
                if (!CheckAdminToken(context)) return;
                await context.Response.WriteAsync(MemoryCache.Default.Aggregate(string.Empty,
                    (current, item) =>
                        current +
                        $"{item.Key.ToUpper()}:" +
                        $"{(item.Value as List<DnsRecordBase> ?? new List<DnsRecordBase> {new TxtRecord(DomainName.Parse("PASS"), 600, item.Value.ToString())}).FirstOrDefault()}" +
                        Environment.NewLine));
            });
            endpoints.Map(Config.AdminPerfix + "/cache/keys", async context =>
            {
                if (!CheckAdminToken(context)) return;
                await context.Response.WriteAsync(string.Join(Environment.NewLine,
                    MemoryCache.Default.Select(item => $"{item.Key}:{item.Value}").ToList()));
            });
            endpoints.Map(Config.AdminPerfix + "/cnlist/ls", async context =>
            {
                if (!CheckAdminToken(context)) return;
                await context.Response.WriteAsync(string.Join(Environment.NewLine, DNSChina.ChinaList));
            });
            endpoints.Map(Config.AdminPerfix + "/cache/rm", async context =>
            {
                if (!CheckAdminToken(context)) return;
                MemoryCache.Default.Trim(100);
                await context.Response.WriteAsync("Trim OK");
            });
            endpoints.Map(Config.AdminPerfix + "/set-token", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";
                if (context.Request.Query.TryGetValue("t", out var tokenValue))
                {
                    context.Response.Cookies.Append("atoken", tokenValue.ToString(),
                        new CookieOptions
                        {
                            Path = "/",
                            HttpOnly = true,
                            MaxAge = TimeSpan.FromDays(30),
                            SameSite = SameSiteMode.Strict,
                            IsEssential = true
                        });
                    await context.Response.WriteAsync("Set OK");
                }
                else await context.Response.WriteAsync("Token Required");
            });
        }

        public static bool CheckAdminToken(HttpContext context)
        {
            context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
            context.Response.ContentType = "text/plain";
            if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                tokenValue.Equals(Config.AdminToken) && Config.UseAdminRoute) return true;
            context.Response.ContentType = "text/plain";
            context.Response.WriteAsync("Token Required");
            return false;
        }
    }
}
