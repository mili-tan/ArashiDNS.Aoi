using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        public static RecursiveDnsResolver RecursiveResolver = new RecursiveDnsResolver();

        public static DefaultObjectPool<DnsClient> UpPool = new(new DnsClientPooledObjectPolicy(
            new[]
            {
                UpEndPoint.Address, BackUpEndPoint.Port == UpEndPoint.Port ? BackUpEndPoint.Address : IPAddress.Any
            }, Config.TimeOut, UpEndPoint.Port), 30);

        public static DefaultObjectPool<DnsClient> BackUpPool = new(
            new DnsClientPooledObjectPolicy(new[] {BackUpEndPoint.Address}, Config.TimeOut, BackUpEndPoint.Port), 30);


        public static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.ReslovePerfix, async context => await MapDnsRoute(context));
            endpoints.Map(Config.QueryPerfix, async context => await MapDnsRoute(context));

            endpoints.Map("/ToHosts/{*path}",
                async context =>
                {
                    try
                    {
                        var res = await new HttpClient() {MaxResponseContentBufferSize = 1024000}.GetStringAsync(
                            "https://" +
                            context.GetRouteValue("path"));
                        var sp = res.Split('\n').Where(x => !x.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(x));

                        if (res.Contains("DOMAIN,"))
                            sp = sp.Where(x => x.StartsWith("DOMAIN,")).Select(x => x.Split(',').Last()).ToList();

                        var str = string.Join(Environment.NewLine, sp.Select(x => "127.0.0.1 " + x));
                        await context.WriteResponseAsync(str);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });

            endpoints.Map("/FilesToHosts/{files}/{*path}",
                async context =>
                {
                    try
                    {
                        var str = string.Empty;
                        foreach (var file in context.GetRouteValue("files").ToString().Split(',', '_'))
                        {
                            var res = await new HttpClient() {MaxResponseContentBufferSize = 10240}.GetStringAsync(
                                "https://" +
                                context.GetRouteValue("path") + "/" + file);
                            var sp = res.Split('\n')
                                .Where(x => !x.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(x))
                                .ToList();

                            if (res.Contains("DOMAIN,"))
                                sp = sp.Where(x => x.StartsWith("DOMAIN,")).Select(x => x.Split(',').Last()).ToList();

                            str += sp.Aggregate(string.Empty,
                                (current, item) => current + ("127.0.0.1 " + item + Environment.NewLine));
                        }

                        await context.WriteResponseAsync(str);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
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

        public static async Task MapDnsRoute(HttpContext context)
        {
            var queryDictionary = context.Request.Query;
            var returnMsg = true;
            var idTe = GetIdTeEnable(context);

            DnsMessage qMsg;

            try
            {
                if (context.Request.Method == "POST")
                {
                    returnMsg = true;
                    qMsg = await DNSParser.FromPostByteAsync(context);
                }
                else if (queryDictionary.ContainsKey("dns"))
                {
                    returnMsg = true;
                    qMsg = DNSParser.FromWebBase64(context);
                }
                else if (queryDictionary.ContainsKey("name"))
                {
                    returnMsg = false;
                    qMsg = DNSParser.FromDnsJson(context, EcsDefaultMask: Config.EcsDefaultMask);
                }
                else
                {
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
                    return;
                }

                if (qMsg == null || !qMsg.Questions.Any())
                {
                    var msg = new DnsMessage
                    {
                        IsRecursionAllowed = true,
                        IsRecursionDesired = true,
                        ReturnCode = ReturnCode.ServerFailure
                    };
                    msg.Questions.Add(new DnsQuestion(DomainName.Root, RecordType.A, RecordClass.INet));
                    msg.AuthorityRecords.Add(new TxtRecord(DomainName.Parse("error.arashi-msg"), 0,
                        "Parse error or invalid query"));
                    await ReturnContext(context, returnMsg, msg, null,
                        transIdEnable: idTe.idEnable, trimEnable: idTe.teEnable);
                    return;
                }
            }
            catch (Exception e)
            {
                var msg = new DnsMessage
                {
                    IsRecursionAllowed = true,
                    IsRecursionDesired = true,
                    ReturnCode = ReturnCode.ServerFailure
                };
                msg.Questions.Add(new DnsQuestion(DomainName.Root, RecordType.A, RecordClass.INet));
                msg.AuthorityRecords.Add(new TxtRecord(DomainName.Parse("error.arashi-msg"), 0,
                    "Fail parse query parameter"));
                await ReturnContext(context, returnMsg, msg, null,
                    transIdEnable: idTe.idEnable, trimEnable: idTe.teEnable);
                Console.WriteLine(e);
                return;
            }

            if (qMsg.Questions.First().RecordClass == RecordClass.Chaos && qMsg.Questions.First().RecordType == RecordType.Txt &&
                qMsg.Questions.First().Name.IsEqualOrSubDomainOf(DomainName.Parse("version.bind")))
            {
                var msg = qMsg.CreateResponseInstance();
                msg.IsRecursionAllowed = true;
                msg.IsRecursionDesired = true;
                msg.AnswerRecords.Add(
                    new TxtRecord(qMsg.Questions.First().Name, 3600, "ArashiDNS.Aoi"));

                await ReturnContext(context, returnMsg, msg, qMsg,
                    transIdEnable: idTe.idEnable, trimEnable: idTe.teEnable);
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
                    transIdEnable: idTe.idEnable, trimEnable: idTe.teEnable);
                return;
            }

            var aMsg = await DnsQuery(qMsg, context);
            await ReturnContext(context, returnMsg, aMsg, qMsg,
                transIdEnable: idTe.idEnable, trimEnable: idTe.teEnable);
        }

        public static async Task ReturnContext(HttpContext context, bool returnMsg, DnsMessage aMsg,
            DnsMessage qMsg = null,
            bool transIdEnable = false, bool trimEnable = false, ushort id = 0)
        {
            try
            {
                var queryDictionary = context.Request.Query;
                var pddingEnable = queryDictionary.ContainsKey("random_padding");
                if (aMsg == null)
                {
                    aMsg = qMsg.CreateResponseInstance();
                    aMsg.ReturnCode = ReturnCode.ServerFailure;
                    aMsg.AuthorityRecords.Add(new TxtRecord(DomainName.Parse("error.arashi-msg"), 0,
                        "Remote DNS server timeout"));
                    //await context.WriteResponseAsync("Remote DNS server timeout",
                    //    StatusCodes.Status500InternalServerError);
                    //return;
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
            bool cnDns = true, bool useCache = true) //, IPAddress ipAddress = null
        {
            try
            {
                var querys = context.Request.Query;

                if (Config.ChinaListEnable && !querys.ContainsKey("no-cndns") && cnDns &&
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

            //if (ipAddress == null || IPAddress.Any.Equals(ipAddress)) //IPAddress.IsLoopback(ipAddress)
            //    ipAddress = UpEndPoint.Address;

            var res = await DnsQuery(dnsMessage, false) ?? await DnsQuery(dnsMessage, true);
            if (res == null || res.ReturnCode == ReturnCode.Refused || res.ReturnCode == ReturnCode.ServerFailure)
            {
                dnsMessage.EDnsOptions.Options.Clear();
                res = await DnsQuery(dnsMessage, true);
            }

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

        public static async Task<DnsMessage> DnsQuery(DnsMessage dnsMessage, bool isBackup)
        {
            if (Config.UseRecursive)
            {
                try
                {
                    var quest = dnsMessage.Questions.FirstOrDefault();
                    var bases = await RecursiveResolver.ResolveAsync<DnsRecordBase>(quest.Name, quest.RecordType,
                        quest.RecordClass);
                    if (bases.Any())
                    {
                        var rMessage = dnsMessage.CreateResponseInstance();
                        rMessage.ReturnCode = ReturnCode.NoError;
                        rMessage.AnswerRecords.AddRange(bases);
                        return rMessage;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            var client = isBackup ? BackUpPool.Get() : UpPool.Get();

            if (dnsMessage.IsEDnsEnabled &&
                (Equals(isBackup ? BackUpEndPoint.Address : UpEndPoint.Address, IPAddress.Parse("1.1.1.1")) ||
                 Equals(isBackup ? BackUpEndPoint.Address : UpEndPoint.Address, IPAddress.Parse("1.0.0.1"))))
            {
                dnsMessage.EDnsOptions.Options.RemoveAll(x => x.Type == EDnsOptionType.ClientSubnet);
                dnsMessage.EDnsOptions.Options.Add(new ClientSubnetOption(0, IPAddress.Any));
            }

            var aMessage = await client.SendMessageAsync(dnsMessage);
            if (isBackup) BackUpPool.Return(client);
            else UpPool.Return(client);

            //new UdpClientTransport(BackUpEndPoint.Port)
            if (aMessage == null)
                return await new DnsClient(new[] {BackUpEndPoint.Address, UpEndPoint.Address},
                        [new TcpClientTransport(BackUpEndPoint.Port)],
                        false, Config.TimeOut)
                    .SendMessageAsync(dnsMessage);

            if (Config.RetellEcsEnable && aMessage.IsEDnsEnabled && dnsMessage.IsEDnsEnabled &&
                aMessage.EDnsOptions.Options.All(x => x.Type != EDnsOptionType.ClientSubnet) &&
                dnsMessage.EDnsOptions.Options.Any(x => x.Type == EDnsOptionType.ClientSubnet))
                aMessage.EDnsOptions.Options.Add(dnsMessage.EDnsOptions.Options.FirstOrDefault(x =>
                    x.Type == EDnsOptionType.ClientSubnet));

            return aMessage;
        }

        public static bool GetClientType(IQueryCollection queryDictionary, string key)
        {
            return queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains(key);
        }

        public static (bool idEnable, bool teEnable) GetIdTeEnable(HttpContext context, bool forceId = false)
        {
            var userAgent = context.Request.Headers.UserAgent.ToString().ToLower();
            var idEnable = Config.TransIdEnable;
            var teEnable = Config.TrimEndEnable;
            var needTeUaList = new HashSet<string> { "mikrotik" };
            var noIdUaList = new HashSet<string> { "intra", "chrome", "curl" };
            var needIdUaList = new HashSet<string>
                {"go-http-client", "dnscrypt", "dalvik", "ikuaios", "clash", "mihomo", "quic-go"};
            var noTeUaList = new HashSet<string>
                {"dalvik", "dns-over-tls", "dns-over-quic", "quic-go", "quic-go", "sing-box"};

            if (noTeUaList.Any(item => userAgent.Contains(item))) teEnable = false;
            else if (needTeUaList.Any(item => userAgent.Contains(item))) teEnable = true;

            if (forceId) idEnable = true;

            if (string.IsNullOrWhiteSpace(userAgent)) return (idEnable, teEnable);
            if (noIdUaList.Any(item => userAgent.Contains(item))) idEnable = false;
            else if (needIdUaList.Any(item => userAgent.Contains(item))) idEnable = true;

            return (idEnable, teEnable);
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
