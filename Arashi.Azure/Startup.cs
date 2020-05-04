using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Arashi.Kestrel;
using Arashi.Kestrel.DNS;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Arashi.Azure
{
    public class Startup
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static string IndexStr = File.Exists(SetupBasePath + "index.html")
            ? File.ReadAllText(SetupBasePath + "index.html")
            : "Welcome to ArashiDNS.P ONE Azure";

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            DnsEncoder.Init();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                });
            }).UseEndpoints(DnsQueryRoute);
        }

        private static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map("/dns-query", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE");
                var queryDictionary = context.Request.Query;
                if (queryDictionary.ContainsKey("dns"))
                    ReturnContext(context, true, DnsQuery(DNSGet.FromWebBase64(queryDictionary["dns"].ToString())));
                else if (queryDictionary.ContainsKey("name"))
                    ReturnContext(context, false, DnsQuery(DNSGet.FromQueryContext(context)));
                else
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                }
            });
        }

        public static async void ReturnContext(HttpContext context, bool returnMsg, DnsMessage dnsMsg)
        {
            var queryDictionary = context.Request.Query;

            if (dnsMsg == null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Remote DNS server timeout");
                return;
            }

            if (returnMsg)
            {
                if (queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains("json"))
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(DohJsonEncoder.Encode(dnsMsg).ToString(Formatting.None));
                }
                else
                {
                    context.Response.ContentType = "application/dns-message";
                    await context.Response.Body.WriteAsync(DnsEncoder.Encode(dnsMsg));
                }
            }
            else
            {
                if (queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains("message"))
                {
                    context.Response.ContentType = "application/dns-message";
                    await context.Response.Body.WriteAsync(DnsEncoder.Encode(dnsMsg));
                }
                else
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(DohJsonEncoder.Encode(dnsMsg).ToString(Formatting.None));
                }
            }

            WriteLogCache(dnsMsg);
        }

        public static DnsMessage DnsQuery(DnsMessage dnsMessage)
        {
            try
            {
                if (DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                if (DNSChina.IsChinaName(dnsMessage.Questions.FirstOrDefault().Name))
                    return DNSChina.ResolveOverHttpDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var client = new DnsClient(IPAddress.Parse("8.8.8.8"), 500);
            for (var i = 0; i < 3; i++)
            {
                var aMessage = client.SendMessage(dnsMessage);
                if (aMessage != null) return aMessage;
            }

            return new DnsClient(IPAddress.Parse("8.8.8.8"), 1000)
                { IsTcpEnabled = true, IsUdpEnabled = false }.SendMessage(dnsMessage);
        }

        public static void WriteLogCache(DnsMessage dnsMessage)
        {
            Task.Run(() => DnsCache.Add(dnsMessage));
        }
    }
}
