using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Arashi.Aoi.DNS;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Arashi.AoiConfig;

namespace Arashi.Aoi.Routes
{
    class DNSRoutes
    {
        public static IPEndPoint UpEndPoint = IPEndPoint.Parse(Config.UpStream);
        public static IPEndPoint BackUpEndPoint = IPEndPoint.Parse(Config.BackUpStream);

        public static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                var queryDictionary = context.Request.Query;
                if (context.Request.Method == "POST")
                    await ReturnContext(context, true,
                        DnsQuery(await DNSParser.FromPostByteAsync(context),
                            context));
                else if (queryDictionary.ContainsKey("dns"))
                    await ReturnContext(context, true,
                        DnsQuery(DNSParser.FromWebBase64(context),
                            context));
                else if (queryDictionary.ContainsKey("name"))
                    await ReturnContext(context, false,
                        DnsQuery(DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask),
                            context));
                else
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
            });
        }

        public static async Task ReturnContext(HttpContext context, bool returnMsg, DnsMessage dnsMsg,
            bool cache = true, bool transId = false)
        {
            try
            {
                var queryDictionary = context.Request.Query;
                if (dnsMsg == null)
                {
                    await context.WriteResponseAsync("Remote DNS server timeout",
                        StatusCodes.Status500InternalServerError);
                    return;
                }

                if (returnMsg)
                {
                    if (GetClientType(queryDictionary, "json"))
                        await context.WriteResponseAsync(DnsJsonEncoder.Encode(dnsMsg).ToString(Formatting.None),
                            type: "application/json", headers: Startup.HeaderDict);
                    else
                        await context.WriteResponseAsync(
                            DnsEncoder.Encode(dnsMsg, transId),
                            type: "application/dns-message");
                }
                else
                {
                    if (GetClientType(queryDictionary, "message"))
                        await context.WriteResponseAsync(
                            DnsEncoder.Encode(dnsMsg, transId),
                            type: "application/dns-message");
                    else
                        await context.WriteResponseAsync(DnsJsonEncoder.Encode(dnsMsg).ToString(Formatting.None),
                            type: "application/json", headers: Startup.HeaderDict);
                }

                if (cache) WriteLogCache(dnsMsg, context);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
                    return DNSChina.ResolveOverChinaDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return DnsQuery(UpEndPoint.Address, dnsMessage, UpEndPoint.Port, Config.TimeOut) ??
                   DnsQuery(BackUpEndPoint.Address, dnsMessage, BackUpEndPoint.Port, Config.TimeOut);
        }

        public static DnsMessage DnsQuery(IPAddress ipAddress, DnsMessage dnsMessage, int port = 53, int timeout = 1500)
        {
            if (port == 0) port = 53;
            var client = new ARSoft.Tools.Net.Dns.DnsClient(ipAddress, timeout)
                {IsUdpEnabled = !Config.OnlyTcpEnable, IsTcpEnabled = true};
            for (var i = 0; i < Config.Retries; i++)
            {
                var aMessage = client.SendMessage(dnsMessage);
                if (aMessage != null) return aMessage;
            }

            return new ARSoft.Tools.Net.Dns.DnsClient(ipAddress, timeout, port)
                {IsTcpEnabled = true, IsUdpEnabled = false}.SendMessage(dnsMessage);
        }

        public static bool GetClientType(IQueryCollection queryDictionary, string key)
        {
            return queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains(key);
        }

        public static void WriteLogCache(DnsMessage dnsMessage, HttpContext context = null)
        {
            if (Config.CacheEnable)
                Task.Run(() =>
                {
                    if (context != null && Config.GeoCacheEnable) DnsCache.Add(dnsMessage, context);
                    else DnsCache.Add(dnsMessage);
                });

            if (Config.RankEnable)
                Task.Run(() =>
                {
                    DNSRank.AddUp(dnsMessage.AnswerRecords.FirstOrDefault().Name);
                    if (context != null && Config.GeoCacheEnable) DNSRank.AddUpGeo(dnsMessage, context);
                });

            if (Config.LogEnable)
                Task.Run(() =>
                {
                    var ip = RealIP.GetFromDns(dnsMessage, context);
                    if (Startup.LoggerFactory != null && Config.FullLogEnable)
                    {
                        var logger = Startup.LoggerFactory.CreateLogger("Arashi.Aoi");
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
