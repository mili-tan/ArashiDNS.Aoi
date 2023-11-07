using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using static Arashi.AoiConfig;
using DnsClient = ARSoft.Tools.Net.Dns.DnsClient;

namespace Arashi.Aoi.Routes
{
    class DnsQueryRoutes
    {
        public static IPEndPoint UpEndPoint = IPEndPoint.Parse(Config.UpStream);
        public static IPEndPoint BackUpEndPoint = IPEndPoint.Parse(Config.BackUpStream);

        public static DefaultObjectPool<DnsClient> UpPool = new(
            new DnsClientPooledObjectPolicy(UpEndPoint.Address, Config.TimeOut, UpEndPoint.Port), 30);

        public static DefaultObjectPool<DnsClient> BackUpPool = new(
            new DnsClientPooledObjectPolicy(BackUpEndPoint.Address, Config.TimeOut, BackUpEndPoint.Port), 30);


        public static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                var queryDictionary = context.Request.Query;

                DnsMessage qMsg;
                bool returnMsg;

                try
                {
                    if (context.Request.Method == "POST")
                    {
                        qMsg = await DNSParser.FromPostByteAsync(context);
                        returnMsg = true;
                    }
                    else if (queryDictionary.ContainsKey("dns"))
                    {
                        qMsg = DNSParser.FromWebBase64(context);
                        returnMsg = true;
                    }
                    else if (queryDictionary.ContainsKey("name"))
                    {
                        qMsg = DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask);
                        returnMsg = false;
                    }
                    else
                    {
                        await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
                        return;
                    }

                    if (qMsg == null || !qMsg.Questions.Any())
                    {
                        await context.WriteResponseAsync("Parse error or invalid query",
                            StatusCodes.Status500InternalServerError);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await context.WriteResponseAsync("Fail parse query parameter",
                        StatusCodes.Status500InternalServerError);
                    return;
                }

                if (qMsg.Questions.First().RecordType == RecordType.Any && !Config.AnyTypeEnable)
                {
                    var msg = qMsg.CreateResponseInstance();
                    msg.IsRecursionAllowed = true;
                    msg.IsRecursionDesired = true;
                    msg.AnswerRecords.Add(
                        new HInfoRecord(qMsg.Questions.First().Name, 3600, "ANY Obsoleted", "RFC8482"));

                    await ReturnContext(context, returnMsg, msg, qMsg,
                        transIdEnable: GetIdEnable(context), id: qMsg.TransactionID);
                    return;
                }

                var aMsg = await DnsQuery(qMsg, context);
                await ReturnContext(context, returnMsg, aMsg, qMsg,
                    transIdEnable: GetIdEnable(context), id: qMsg.TransactionID);
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

        public static async Task ReturnContext(HttpContext context, bool returnMsg, DnsMessage aMsg, DnsMessage qMsg = null,
            bool transIdEnable = false, bool trimEnable = false, ushort id = 0)
        {
            try
            {
                var queryDictionary = context.Request.Query;
                var pddingEnable = queryDictionary.ContainsKey("random_padding");
                if (aMsg == null)
                {
                    await context.WriteResponseAsync("Remote DNS server timeout",
                        StatusCodes.Status500InternalServerError);
                    return;
                }
                if (qMsg != null)
                {
                    var response = qMsg.CreateResponseInstance();
                    response.ReturnCode = aMsg.ReturnCode;
                    response.IsRecursionAllowed = true;
                    response.IsRecursionDesired = true;
                    if (aMsg.AnswerRecords.Any()) response.AnswerRecords.AddRange(aMsg.AnswerRecords);
                    if (aMsg.AuthorityRecords.Any()) response.AuthorityRecords.AddRange(aMsg.AuthorityRecords);
                    aMsg = response;
                }

                returnMsg = returnMsg
                    ? !GetClientType(queryDictionary, "json")
                    : GetClientType(queryDictionary, "message");

                if (returnMsg)
                    await context.WriteResponseAsync(
                        DnsEncoder.Encode(aMsg, transIdEnable, trimEnable, id),
                        type: "application/dns-message");
                else
                    await context.WriteResponseAsync(
                        DnsJsonEncoder.Encode(aMsg, pddingEnable)
                            .ToString(Formatting.None),
                        type: "application/json", headers: Startup.HeaderDict);

                WriteLog(aMsg, context);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, HttpContext context,
            bool cnDns = true, bool useCache = true, IPAddress ipAddress = null)
        {
            try
            {
                var querys = context.Request.Query;

                if (Config.ChinaListEnable && !querys.ContainsKey("no-cndns") && cnDns &&
                    dnsMessage.Questions.FirstOrDefault()!.RecordType == RecordType.A &&
                    await DNSChina.IsChinaNameAsync(dnsMessage.Questions.FirstOrDefault().Name))
                {
                    if (Config.GeoCacheEnable && DnsCache.Contains(dnsMessage, context, "CN"))
                        return DnsCache.Get(dnsMessage, context, "CN");
                    if (DnsCache.Contains(dnsMessage, tag: "CN")) return DnsCache.Get(dnsMessage, tag: "CN");

                    var cnres = await DNSChina.ResolveOverChinaDns(dnsMessage);
                    WriteCache(cnres, context, "CN");
                    return cnres;
                }

                if (Config.CacheEnable && !querys.ContainsKey("no-cache") && useCache)
                {
                    if (Config.GeoCacheEnable && DnsCache.Contains(dnsMessage, context))
                        return DnsCache.Get(dnsMessage, context);
                    if (DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (ipAddress == null || IPAddress.Any.Equals(ipAddress)) //IPAddress.IsLoopback(ipAddress)
                ipAddress = UpEndPoint.Address;

            var res = await DnsQuery(dnsMessage, false) ?? await DnsQuery(dnsMessage, true);
            if (res.ReturnCode == ReturnCode.Refused) res = await DnsQuery(dnsMessage, true);

            WriteCache(res, context);
            return res;
        }

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, bool cnDns = true, bool useCache = true)
        {
            try
            {
                if (Config.CacheEnable && useCache && DnsCache.Contains(dnsMessage)) return DnsCache.Get(dnsMessage);
                if (Config.ChinaListEnable && cnDns &&
                    await DNSChina.IsChinaNameAsync(dnsMessage.Questions.FirstOrDefault().Name) &&
                    dnsMessage.Questions.FirstOrDefault().RecordType == RecordType.A)
                    return await DNSChina.ResolveOverChinaDns(dnsMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var res = await DnsQuery(dnsMessage, false) ?? await DnsQuery(dnsMessage, true);
            if (res.ReturnCode == ReturnCode.Refused) res = await DnsQuery(dnsMessage, true);
            return res;
        }

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, bool isBack)
        {
            var client = isBack ? BackUpPool.Get() : UpPool.Get();
            for (var i = 0; i < Config.Retries; i++)
            {
                var aMessage = await client.SendMessageAsync(dnsMessage);
                if (aMessage != null) return aMessage;
            }

            if (isBack) BackUpPool.Return(client);
            else UpPool.Return(client);

            return await new DnsClient(isBack ? BackUpEndPoint.Address : UpEndPoint.Address, Config.TimeOut,
                    isBack ? BackUpEndPoint.Port : UpEndPoint.Port)
                {IsTcpEnabled = true, IsUdpEnabled = false}.SendMessageAsync(dnsMessage);
        }

        public static bool GetClientType(IQueryCollection queryDictionary, string key)
        {
            return queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains(key);
        }

        public static bool GetIdEnable(HttpContext context)
        {
            var queryDictionary = context.Request.Query;
            var userAgent = context.Request.Headers.UserAgent.ToString().ToLower();

            var idEnable = Config.TransIdEnable;
            var noIdUaList = new List<string> {"intra", "chrome", "curl"};
            var needIdUaList = new List<string> {"go-http-client", "dnscrypt-proxy", "dnscrypt"};

            if (queryDictionary.TryGetValue("idEnable", out var str) && bool.TryParse(str, out var idResult))
                idEnable = idResult;
            else if (!string.IsNullOrWhiteSpace(userAgent))
            {
                if (noIdUaList.Any(item => userAgent.Contains(item)))
                    idEnable = false;
                else if (needIdUaList.Any(item => userAgent.Contains(item)))
                    idEnable = true;
            }
            else
                idEnable = true;

            return idEnable;
        }

        public static void WriteLog(DnsMessage dnsMessage, HttpContext context = null)
        {
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

        public static void WriteCache(DnsMessage res, HttpContext context, string tag = "")
        {
            if (Config.CacheEnable && res != null)
                Task.Run(() =>
                {
                    if (context != null && Config.GeoCacheEnable) DnsCache.Add(res, context, tag);
                    else DnsCache.Add(res, tag);
                });
        }
    }
}
