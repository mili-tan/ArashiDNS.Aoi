using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;
using static Arashi.AoiConfig;

namespace Arashi
{
    class DNSParser
    {
        public static DnsMessage FromDnsJson(HttpContext context, bool ActiveEcs = true, byte EcsDefaultMask = 24)
        {
            var queryDictionary = context.Request.Query;
            var dnsQuestion = new DnsQuestion(DomainName.Parse(queryDictionary["name"]), RecordType.A,
                RecordClass.INet);
            if (queryDictionary.ContainsKey("type"))
                if (Enum.TryParse(queryDictionary["type"], true, out RecordType rType))
                    dnsQuestion = new DnsQuestion(DomainName.Parse(queryDictionary["name"]), rType,
                        RecordClass.INet);

            var dnsQMsg = new DnsMessage
            {
                IsEDnsEnabled = true,
                IsQuery = true,
                IsRecursionAllowed = true,
                IsRecursionDesired = true,
                TransactionID = Convert.ToUInt16(new Random(DateTime.Now.Millisecond).Next(1, 99))
            };
            dnsQMsg.Questions.Add(dnsQuestion);
            if (!Config.EcsEnable || !ActiveEcs || queryDictionary.ContainsKey("no-ecs")) return dnsQMsg;

            if (queryDictionary.ContainsKey("edns_client_subnet"))
            {
                var ipStr = queryDictionary["edns_client_subnet"].ToString();
                var ipNetwork = ipStr.Contains("/")
                    ? IPNetwork.Parse(ipStr)
                    : IPNetwork.Parse(ipStr, EcsDefaultMask);
                dnsQMsg.EDnsOptions.Options.Add(new ClientSubnetOption(ipNetwork.Cidr, ipNetwork.Network));
            }
            else
                dnsQMsg.EDnsOptions.Options.Add(
                    new ClientSubnetOption(EcsDefaultMask,
                        IPNetwork.Parse(RealIP.Get(context).ToString(), EcsDefaultMask).Network));

            return dnsQMsg;
        }

        public static DnsMessage FromWebBase64(string base64) => DnsMessage.Parse(DecodeWebBase64(base64));

        public static DnsMessage FromWebBase64(HttpContext context, bool ActiveEcs = true, byte EcsDefaultMask = 24)
        {
            var msg = FromWebBase64(context.Request.Query["dns"].ToString());
            if (!Config.EcsEnable || !ActiveEcs || context.Request.Query.ContainsKey("no-ecs")) return msg;
            if (IsEcsEnable(msg)) return msg;
            if (!msg.IsEDnsEnabled) msg.IsEDnsEnabled = true;
            msg.EDnsOptions.Options.Add(new ClientSubnetOption(EcsDefaultMask,
                IPNetwork.Parse(RealIP.Get(context).ToString(), EcsDefaultMask).Network));
            return msg;
        }

        public static async Task<DnsMessage> FromPostByteAsync(HttpContext context, bool ActiveEcs = true,
            byte EcsDefaultMask = 24)
        {
            var msg = DnsMessage.Parse((await context.Request.BodyReader.ReadAsync()).Buffer.ToArray());
            if (!Config.EcsEnable || !ActiveEcs || context.Request.Query.ContainsKey("no-ecs")) return msg;
            if (IsEcsEnable(msg)) return msg;
            if (!msg.IsEDnsEnabled) msg.IsEDnsEnabled = true;
            msg.EDnsOptions.Options.Add(new ClientSubnetOption(EcsDefaultMask,
                IPNetwork.Parse(RealIP.Get(context).ToString(), EcsDefaultMask).Network));
            return msg;
        }

        public static bool IsEcsEnable(DnsMessage msg)
        {
            return msg.IsEDnsEnabled && msg.EDnsOptions.Options.ToArray().OfType<ClientSubnetOption>().Any();
        }
        public static byte[] DecodeWebBase64(string base64)
        {
            if (base64.Length % 4 > 0) base64 = base64.PadRight(base64.Length + 4 - base64.Length % 4, '=');
            return Convert.FromBase64String(base64.Replace("-", "+").Replace("_", "/"));
        }
    }
}
