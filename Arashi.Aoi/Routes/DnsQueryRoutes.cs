using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Arashi.Aoi.DNS;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Arashi.AoiConfig;
using DnsClient = ARSoft.Tools.Net.Dns.DnsClient;

namespace Arashi.Aoi.Routes
{
    class DnsQueryRoutes
    {
        public static IPEndPoint UpEndPoint = IPEndPoint.Parse(Config.UpStream);
        public static IPEndPoint BackUpEndPoint = IPEndPoint.Parse(Config.BackUpStream);

        public static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                var queryDictionary = context.Request.Query;
                var userAgent = context.Request.Headers.UserAgent.ToString().ToLower();
                
                var idEnable = Config.TransIdEnable;
                var noIdUaList = new List<string> {"intra", "chrome", "curl"};
                var needIdUaList = new List<string> { "go-http-client", "dnscrypt-proxy", "dnscrypt" };

                if (!string.IsNullOrWhiteSpace(userAgent))
                {
                    if (noIdUaList.Any(item => userAgent.Contains(item)))
                        idEnable = false;
                    else if (needIdUaList.Any(item => userAgent.Contains(item)))
                        idEnable = true;
                }
                else
                    idEnable = true;

                if (context.Request.Method == "POST")
                {
                    var dnsq = await DNSParser.FromPostByteAsync(context);
                    await ReturnContext(context, true,
                        await DnsQuery(dnsq, context),
                        transIdEnable: idEnable, id: dnsq.TransactionID);
                }
                else if (queryDictionary.ContainsKey("dns"))
                {
                    var dnsq = DNSParser.FromWebBase64(context);
                    await ReturnContext(context, true,
                        await DnsQuery(dnsq, context),
                        transIdEnable: idEnable, id: dnsq.TransactionID);
                }
                else if (queryDictionary.ContainsKey("name"))
                    await ReturnContext(context, false,
                        await DnsQuery(DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask), context),
                        transIdEnable: idEnable);
                else
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
            });

            endpoints.Map("/refresh-dns", async context =>
            {
                var ip = RealIP.Get(context);
                if (Enum.TryParse(context.Request.Query["type"].ToString(), true, out RecordType type) &&
                    DomainName.TryParse(context.Request.Query["name"].ToString(), out var name))
                {
                    DnsCache.Remove(name, type, ip);
                    await context.WriteResponseAsync(
                        JsonConvert.SerializeObject(new {status = "OK", type, domain = name.ToString()},
                            Formatting.Indented),
                        StatusCodes.Status200OK, "application/json");
                }
                else
                    await context.WriteResponseAsync("Invalid query", StatusCodes.Status403Forbidden);
            });
        }

        public static async Task ReturnContext(HttpContext context, bool returnMsg, DnsMessage dnsMsg,
            bool cacheEnable = true, bool transIdEnable = false, bool trimEnable = false, ushort id = 0)
        {
            try
            {
                var queryDictionary = context.Request.Query;
                var randomPddingKey = queryDictionary.ContainsKey("random_padding");
                if (dnsMsg == null)
                {
                    await context.WriteResponseAsync("Remote DNS server timeout",
                        StatusCodes.Status500InternalServerError);
                    return;
                }

                if (returnMsg)
                {
                    if (GetClientType(queryDictionary, "json"))
                        await context.WriteResponseAsync(
                            DnsJsonEncoder.Encode(dnsMsg, randomPddingKey)
                                .ToString(Formatting.None),
                            type: "application/json", headers: Startup.HeaderDict);
                    else
                        await context.WriteResponseAsync(
                            DnsEncoder.Encode(dnsMsg, transIdEnable, trimEnable, id),
                            type: "application/dns-message");
                }
                else
                {
                    if (GetClientType(queryDictionary, "message"))
                        await context.WriteResponseAsync(
                            DnsEncoder.Encode(dnsMsg, transIdEnable, trimEnable, id),
                            type: "application/dns-message");
                    else
                        await context.WriteResponseAsync(
                            DnsJsonEncoder.Encode(dnsMsg, randomPddingKey)
                                .ToString(Formatting.None),
                            type: "application/json", headers: Startup.HeaderDict);
                }

                if (cacheEnable) WriteLogCache(dnsMsg, context);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, HttpContext context,
            bool CnDns = true, bool Cache = true, IPAddress ipAddress = null)
        {
            try
            {
                if (Config.CacheEnable && !context.Request.Query.ContainsKey("no-cache") && Cache)
                {
                    if (Config.GeoCacheEnable && DnsCache.Contains(dnsMessage, context))
                        return DnsCache.Get(dnsMessage, context);
                    if (DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                }

                if (Config.ChinaListEnable && !context.Request.Query.ContainsKey("no-cndns") && CnDns &&
                    DNSChina.IsChinaName(dnsMessage.Questions.FirstOrDefault().Name) &&
                    dnsMessage.Questions.FirstOrDefault().RecordType == RecordType.A)
                    return await DNSChina.ResolveOverChinaDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (ipAddress == null || IPAddress.Any.Equals(ipAddress)) //IPAddress.IsLoopback(ipAddress)
                ipAddress = UpEndPoint.Address;

            var res = await DnsQuery(ipAddress, dnsMessage, UpEndPoint.Port, Config.TimeOut) ??
                      await DnsQuery(BackUpEndPoint.Address, dnsMessage, BackUpEndPoint.Port, Config.TimeOut);
            if (res.ReturnCode == ReturnCode.Refused)
                res = await DnsQuery(BackUpEndPoint.Address, dnsMessage, BackUpEndPoint.Port, Config.TimeOut);
            return res;
        }

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, bool CnDns = true, bool Cache = true)
        {
            try
            {
                if (Config.CacheEnable && Cache && DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                if (Config.ChinaListEnable && CnDns &&
                    DNSChina.IsChinaName(dnsMessage.Questions.FirstOrDefault().Name) &&
                    dnsMessage.Questions.FirstOrDefault().RecordType == RecordType.A)
                    return await DNSChina.ResolveOverChinaDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var res = await DnsQuery(UpEndPoint.Address, dnsMessage, UpEndPoint.Port, Config.TimeOut) ??
                      await DnsQuery(BackUpEndPoint.Address, dnsMessage, BackUpEndPoint.Port, Config.TimeOut);
            if (res.ReturnCode == ReturnCode.Refused)
                res = await DnsQuery(BackUpEndPoint.Address, dnsMessage, BackUpEndPoint.Port, Config.TimeOut);
            return res;
        }

        public static async Task<DnsMessage> DnsQuery(IPAddress ipAddress, DnsMessage dnsMessage, int port = 53, int timeout = 1500)
        {
            if (port == 0) port = 53;
            var client = new DnsClient(ipAddress, timeout)
                { IsUdpEnabled = !Config.OnlyTcpEnable, IsTcpEnabled = true };
            for (var i = 0; i < Config.Retries; i++)
            {
                var aMessage = await client.SendMessageAsync(dnsMessage);
                if (aMessage != null) return aMessage;
            }

            return await new DnsClient(ipAddress, timeout, port)
                { IsTcpEnabled = true, IsUdpEnabled = false }.SendMessageAsync(dnsMessage);
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
