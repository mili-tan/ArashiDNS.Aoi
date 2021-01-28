using System;
using System.IO;
using Arashi.Aoi.Routes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Arashi.AoiConfig;

namespace Arashi
{
    public class Startup
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        public static ILoggerFactory LoggerFactory;
        public static HeaderDictionary HeaderDict = new();
        public static string IndexStr = File.Exists(SetupBasePath + "index.html")
            ? File.ReadAllText(SetupBasePath + "index.html")
            : "Welcome to ArashiDNS.P ONE.Aoi Azure";

        public void ConfigureServices(IServiceCollection services)
        {
            DnsEncoder.Init();
            if (File.Exists(SetupBasePath + "headers.list"))
                foreach (var s in File.ReadAllText(SetupBasePath + "headers.list").Split(Environment.NewLine))
                    HeaderDict.Add(s.Split(':')[0], s.Split(':')[1]);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (loggerFactory != null) LoggerFactory = loggerFactory;
            if (Config.UseExceptionPage) app.UseDeveloperExceptionPage();

            app.UseRouting().UseEndpoints(endpoints => endpoints.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(IndexStr);
            })).UseEndpoints(DNSRoutes.DnsQueryRoute);

            if (Config.UseIpRoute) app.UseEndpoints(IPRoutes.GeoIPRoute);
            if (Config.UseAdminRoute) app.UseEndpoints(AdminRoutes.AdminRoute);
        }
    }
}
