using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
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
                if (!await CheckAdminToken(context)) return;
                await context.WriteResponseAsync(MemoryCache.Default.Aggregate(string.Empty,
                    (current, item) =>
                        current +
                        $"{item.Key.ToUpper()}:" +
                        $"{((item.Value as DnsCache.CacheEntity).List ?? new List<DnsRecordBase> {new TxtRecord(DomainName.Parse("PASS"), 600, item.Value.ToString())}).FirstOrDefault()}" +
                        Environment.NewLine));
            });
            endpoints.Map(Config.AdminPerfix + "/cache/keys", async context =>
            {
                if (!await CheckAdminToken(context)) return;
                await context.WriteResponseAsync(string.Join(Environment.NewLine,
                    MemoryCache.Default.Select(item => $"{item.Key}:{item.Value}").ToList()));
            });
            endpoints.Map(Config.AdminPerfix + "/cnlist/ls", async context =>
            {
                if (!await CheckAdminToken(context)) return;
                await context.WriteResponseAsync(string.Join(Environment.NewLine, DNSChina.ChinaList));
            });
            endpoints.Map(Config.AdminPerfix + "/cache/rm", async context =>
            {
                if (!await CheckAdminToken(context)) return;
                MemoryCache.Default.Trim(100);
                await context.WriteResponseAsync("Trim OK");
            });
            endpoints.Map(Config.AdminPerfix + "/set-token", async context =>
            {
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
                    await context.WriteResponseAsync(
                        "<!DOCTYPE html><html><script language=\"javascript\">window.opener=null;window.close();</script></html>",
                        type: "text/html");
                }
                else await context.WriteResponseAsync("Token Required", StatusCodes.Status400BadRequest);
            });
        }

        public static async Task<bool> CheckAdminToken(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                tokenValue.Equals(Config.AdminToken) && Config.UseAdminRoute) return true;
            context.Response.ContentType = "text/plain";
            await context.WriteResponseAsync("Token NotFound", StatusCodes.Status404NotFound);
            return false;
        }
    }
}
