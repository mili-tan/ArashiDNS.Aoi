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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Arashi.Azure
{
    public class Startup
    {
        private static ILoggerFactory LoggerFactory;
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static string IndexStr = File.Exists(SetupBasePath + "index.html")
            ? File.ReadAllText(SetupBasePath + "index.html")
            : "Welcome to ArashiDNS.P ONE.Aoi Azure";

        public void ConfigureServices(IServiceCollection services) => DnsEncoder.Init();

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (loggerFactory != null) LoggerFactory = loggerFactory;
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.UseRouting().UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                });
            }).UseEndpoints(DnsQueryRoute);

            if (Config.UseIpRoute) app.UseEndpoints(GeoIPRoute);
            if (Config.UseAdminRoute) app.UseEndpoints(AdminRoute);
        }

        private static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                var queryDictionary = context.Request.Query;
                if (queryDictionary.ContainsKey("dns"))
                    ReturnContext(context, true, DnsQuery(DNSGet.FromWebBase64(context), context));
                else if (queryDictionary.ContainsKey("name"))
                    ReturnContext(context, false, DnsQuery(DNSGet.FromQueryContext(context), context));
                else
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(IndexStr);
                }
            });
        }

        private static void AdminRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.AdminPerfix + "/cache/ls", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out string tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                    await context.Response.WriteAsync(MemoryCache.Default.Aggregate(string.Empty,
                        (current, item) =>
                            current + $"{item.Key.ToUpper()}:{((List<DnsRecordBase>) item.Value).FirstOrDefault()}" +
                            Environment.NewLine));
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/cnlist/ls", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                    await context.Response.WriteAsync(string.Join(Environment.NewLine, DNSChina.ChinaList));
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/cache/rm", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                {
                    MemoryCache.Default.Trim(100);
                    await context.Response.WriteAsync("Trim OK");
                }
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/set-token", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";
                if (context.Request.Query.TryGetValue("t",out var tokenValue))
                {
                    context.Response.Cookies.Append("atoken", tokenValue.ToString(),
                        new CookieOptions
                        {
                            Path = "/", HttpOnly = true, MaxAge = TimeSpan.FromDays(30),
                            SameSite = SameSiteMode.Strict, IsEssential = true
                        });
                    await context.Response.WriteAsync("Set OK");
                }
                else await context.Response.WriteAsync("Token Required");
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
                    {"IP", RealIP.Get(context)}, {"UserHostAddress", context.Connection.RemoteIpAddress.ToString()}
                };
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xFwdValue))
                    jObject.Add("X-Forwarded-For", xFwdValue.ToString());
                if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var xCfValue))
                    jObject.Add("CF-Connecting-IP", xCfValue.ToString());
                if (context.Request.Headers.TryGetValue("X-Real-IP", out var xRealValue))
                    jObject.Add("X-Real-IP", xRealValue.ToString());
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
                    if (LoggerFactory != null && Config.FullLogEnable)
                    {
                        var logger = LoggerFactory.CreateLogger("Arashi.Aoi");
                        dnsMessage.Questions.ForEach(o => logger.LogInformation(ip + ":Question:" + o));
                        dnsMessage.AnswerRecords.ForEach(o => logger.LogInformation(ip + ":Answer:" + o));
                        dnsMessage.AuthorityRecords.ForEach(o => logger.LogInformation(ip + ":Authority:" + o));
                    }
                    else
                    {
                        dnsMessage.Questions.ForEach(o => Console.WriteLine(ip + ":Question:" + o));
                        dnsMessage.AnswerRecords.ForEach(o => Console.WriteLine(ip + ":Answer:" + o));
                        dnsMessage.AuthorityRecords.ForEach(o => Console.WriteLine(ip + ":Authority:" + o));
                    }
                });
        }
    }
}
