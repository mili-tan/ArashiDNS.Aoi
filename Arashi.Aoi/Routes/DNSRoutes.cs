using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Arashi.Azure;
using Arashi.Kestrel;
using Arashi.Kestrel.DNS;
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
        public static void DnsQueryRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.QueryPerfix, async context =>
            {
                var queryDictionary = context.Request.Query;
                if (context.Request.Method == "POST" && context.Request.ContentType.Contains("dns-message") &&
                    context.Request.BodyReader.TryRead(out var readResult))
                    await ReturnContext(context, true, DnsQuery(DnsMessage.Parse(readResult.Buffer.ToArray()), context));
                else if (queryDictionary.ContainsKey("dns"))
                    await ReturnContext(context, true, DnsQuery(DNSGet.FromWebBase64(context), context));
                else if (queryDictionary.ContainsKey("name"))
                    await ReturnContext(context, false, DnsQuery(DNSGet.FromQueryContext(context), context));
                else
                    await context.WriteResponseAsync(Startup.IndexStr, type: "text/html");
            });
        }

        public static async Task ReturnContext(HttpContext context, bool returnMsg, DnsMessage dnsMsg,
            bool cache = true)
        {
            var queryDictionary = context.Request.Query;

            if (dnsMsg == null)
            {
                await context.WriteResponseAsync("Remote DNS server timeout", StatusCodes.Status500InternalServerError);
                return;
            }

            if (returnMsg)
            {
                if (queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains("json"))
                    await context.WriteResponseAsync(DohJsonEncoder.Encode(dnsMsg).ToString(Formatting.None),
                        type: "application/json", headers: Startup.HeaderDict);
                else
                    await context.WriteResponseAsync(DnsEncoder.Encode(dnsMsg), type: "application/dns-message");
            }
            else
            {
                if (queryDictionary.ContainsKey("ct") && queryDictionary["ct"].ToString().Contains("message"))
                    await context.WriteResponseAsync(DnsEncoder.Encode(dnsMsg), type: "application/dns-message");
                else
                    await context.WriteResponseAsync(DohJsonEncoder.Encode(dnsMsg).ToString(Formatting.None),
                        type: "application/json", headers: Startup.HeaderDict);
            }

            if (cache) WriteLogCache(dnsMsg, context);
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

            return DnsQuery(IPAddress.Parse(Config.UpStream), dnsMessage);
        }

        public static DnsMessage DnsQuery(IPAddress ipAddress, DnsMessage dnsMessage)
        {
            var client = new DnsClient(ipAddress, Config.TimeOut)
                {IsUdpEnabled = !Config.OnlyTcpEnable, IsTcpEnabled = true};
            for (var i = 0; i < Config.Retries; i++)
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
