using System;
using System.IO;
using System.Net.Http;
using System.Timers;
using Arashi.Aoi;
using Arashi.Aoi.Routes;
using ArashiDNS.Tools;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Arashi.AoiConfig;

namespace Arashi
{
    public class Startup
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        public static ILoggerFactory LoggerFactory;
        public static IHttpClientFactory ClientFactory;
        public static HeaderDictionary HeaderDict = new();
        public static string IndexStr = File.Exists(SetupBasePath + "index.html")
            ? File.ReadAllText(SetupBasePath + "index.html")
            : "200 OK";

        public void ConfigureServices(IServiceCollection services)
        {
            if (Config.ChinaListEnable)
            {
                MFaster.Init();
                DNSChina.Init();
            }
            if (Config.CacheEnable && Config.GeoCacheEnable /*|| Config.RankEnable*/) GeoIP.Init();

            if (File.Exists(SetupBasePath + "headers.list"))
                foreach (var s in File.ReadAllText(SetupBasePath + "headers.list").Split(Environment.NewLine))
                    HeaderDict.Add(s.Split(':')[0], s.Split(':')[1]);

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("USEINDEX")) &&
                bool.Parse(Environment.GetEnvironmentVariable("USEINDEX")))
                IndexStr = new HttpClient().GetStringAsync("https://mili.one/arashi/arashi").Result;

            HeaderDict.Add("Access-Control-Allow-Origin", "*");

            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(new ConfigurationBuilder()
                .AddJsonFile("appsettings.json").Build().GetSection("IpRateLimiting"));
            services.AddInMemoryRateLimiting();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddHttpClient("DNSChina", client => client.Timeout = TimeSpan.FromSeconds(1));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
            IHttpClientFactory clientFactory)
        {
            app.Use(async (context, next) =>
            {
                if (!string.IsNullOrWhiteSpace(Config.HostName) && context.Request.Host.Host != Config.HostName)
                    await context.WriteResponseAsync("Host Not Found", 404);
                else await next(context);
            });
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = RealIP.Get(context);
                await next(context);
            });

            if (loggerFactory != null) LoggerFactory = loggerFactory;
            if (clientFactory != null) ClientFactory = clientFactory;
            if (Config.RateLimitingEnable) app.UseIpRateLimiting();
            if (Config.UseExceptionPage) app.UseDeveloperExceptionPage();

            app.UseRouting().UseEndpoints(endpoints => endpoints.MapGet("/",
                    async context =>
                        await context.WriteResponseAsync(IndexStr, type: "text/html", headers: HeaderDict)))
                .UseEndpoints(DnsQueryRoutes.DnsQueryRoute);

            if (Config.UseIpRoute) app.UseEndpoints(IPRoutes.GeoIPRoute);
            if (Config.UseAdminRoute) app.UseEndpoints(AdminRoutes.AdminRoute);
            if (Config.UseErikoRoute) app.UseEndpoints(ErikoRoutes.ErikoRoute);
        }
    }
}
