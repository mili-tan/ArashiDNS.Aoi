using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
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
using Newtonsoft.Json.Linq;

namespace Arashi.Azure
{
    public class Startup
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static string IndexStr = File.Exists(SetupBasePath + "index.html")
            ? File.ReadAllText(SetupBasePath + "index.html")
            : "Welcome to ArashiDNS.P ONE Azure";

        public void ConfigureServices(IServiceCollection services)
        {
            DnsEncoder.Init();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.UseRouting().UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                });
            });
            if (Config.UseIpRoute) app.UseEndpoints(GeoIPRoute);
            if (Config.UseCacheRoute) app.UseEndpoints(CacheRoute);
        }

        private static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE");
                var queryDictionary = context.Request.Query;
                if (queryDictionary.ContainsKey("dns"))
                    ReturnContext(context, true,
                        DnsQuery(DNSGet.FromWebBase64(queryDictionary["dns"].ToString()), context));
                else if (queryDictionary.ContainsKey("name"))
                    ReturnContext(context, false, DnsQuery(DNSGet.FromQueryContext(context), context));
                else
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                }
            });

        }

        private static void CacheRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.CachePerfix + "/ls", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE");
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(MemoryCache.Default.Aggregate(string.Empty,
                    (current, item) =>
                        current + $"{item.Key.ToUpper()}:{((List<DnsRecordBase>)item.Value).FirstOrDefault()}" +
                        Environment.NewLine));
            });
            endpoints.Map(Config.CachePerfix + "/rm", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE");
                context.Response.ContentType = "text/plain";
                MemoryCache.Default.Trim(100);
                await context.Response.WriteAsync("OK");
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

            WriteLogCache(dnsMsg, context);
        }

        private static void GeoIPRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.IpPerfix, async context =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(RealIP.Get(context).ToString());
            });
            endpoints.Map(Config.IpPerfix + "/source", async context =>
            {
                var jObject = new JObject
                {
                    {"IP", RealIP.Get(context)},
                    {
                        "UserHostAddress",
                        context.Connection.RemoteIpAddress.ToString()
                    }
                };
                if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
                    jObject.Add("X-Forwarded-For", context.Request.Headers["X-Forwarded-For"].ToString());
                if (context.Request.Headers.ContainsKey("CF-Connecting-IP"))
                    jObject.Add("CF-Connecting-IP", context.Request.Headers["CF-Connecting-IP"].ToString());
                if (context.Request.Headers.ContainsKey("X-Real-IP"))
                    jObject.Add("X-Real-IP", context.Request.Headers["X-Real-IP"].ToString());
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jObject.ToString());
            });
            endpoints.Map(Config.IpPerfix + "/json", async context =>
            {
                var jObject = new JObject();
                var asnCity = GeoIP.GetAsnCityValueTuple(context.Request.Query.ContainsKey("ip")
                    ? context.Request.Query["ip"].ToString()
                    : RealIP.Get(context));
                var responseAsn = asnCity.Item1;
                var responseCity = asnCity.Item2;
                jObject.Add("IP", responseAsn.IPAddress);
                jObject.Add("ASN", responseAsn.AutonomousSystemNumber);
                jObject.Add("Organization", responseAsn.AutonomousSystemOrganization);
                jObject.Add("CountryCode", responseCity.Country.IsoCode);
                jObject.Add("Country", responseCity.Country.Name);
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.IsoCode))
                    jObject.Add("ProvinceCode", responseCity.MostSpecificSubdivision.IsoCode);
                if (!string.IsNullOrWhiteSpace(responseCity.MostSpecificSubdivision.Name))
                    jObject.Add("Province", responseCity.MostSpecificSubdivision.Name);
                if (!string.IsNullOrWhiteSpace(responseCity.City.Name))
                    jObject.Add("City", responseCity.City.Name);
                var cnIsp = GeoIP.GetCnISP(responseAsn, responseCity);
                if (!string.IsNullOrWhiteSpace(cnIsp)) jObject.Add("ISP", cnIsp);

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(jObject.ToString());
            });
        }

        public static DnsMessage DnsQuery(DnsMessage dnsMessage, HttpContext context = null)
        {
            try
            {
                if (Config.CacheEnable)
                {
                    if (context != null && Config.GeoCacheEnable && DnsCache.Contains(dnsMessage, context))
                        return DnsCache.Get(dnsMessage, context);
                    if (DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                }

                if (Config.ChinaListEnable && DNSChina.IsChinaName(dnsMessage.Questions.FirstOrDefault().Name) &&
                    dnsMessage.Questions.FirstOrDefault().RecordType == RecordType.A)
                    return DNSChina.ResolveOverHttpDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return DnsQuery(Config.UpStream, dnsMessage);
        }

        public static DnsMessage DnsQuery(IPAddress ipAddress, DnsMessage dnsMessage)
        {
            var client = new DnsClient(ipAddress, Config.TimeOut)
                {IsUdpEnabled = !Config.OnlyTcpEnable, IsTcpEnabled = true};
            for (var i = 0; i < Config.Tries; i++)
            {
                var aMessage = client.SendMessage(dnsMessage);
                if (aMessage != null) return aMessage;
            }

            return new DnsClient(ipAddress, Config.TimeOut)
                {IsTcpEnabled = true, IsUdpEnabled = false}.SendMessage(dnsMessage);
        }

        public static void WriteLogCache(DnsMessage dnsMessage, HttpContext context = null)
        {
            if (Config.CacheEnable)
                Task.Run(() =>
                {
                    if (context != null && Config.GeoCacheEnable) DnsCache.Add(dnsMessage, context);
                    else DnsCache.Add(dnsMessage);
                });

            if (Config.LogEnable)
                Task.Run(() =>
                {
                    var ip = RealIP.GetFromDns(dnsMessage, context);
                    dnsMessage.Questions.ForEach(o => Console.WriteLine(ip + ":Question:" + o));
                    dnsMessage.AnswerRecords.ForEach(o => Console.WriteLine(ip + ":Answer:" + o));
                    dnsMessage.AuthorityRecords.ForEach(o => Console.WriteLine(ip + ":Authority:" + o));
                });
        }
    }
}
