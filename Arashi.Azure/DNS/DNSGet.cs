using System;
using System.Net;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Http;

namespace Arashi.Kestrel.DNS
{
    class DNSGet
    {
        public static byte[] DecodeWebBase64(string base64)
        {
            if (base64.Length % 4 > 0) base64 = base64.PadRight(base64.Length + 4 - base64.Length % 4, '=');
            return Convert.FromBase64String(base64.Replace("-", "+").Replace("_", "/"));
        }

        public static DnsMessage FromQueryContext(HttpContext context)
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

            if (queryDictionary.ContainsKey("edns_client_subnet"))
            {
                var ipStr = queryDictionary["edns_client_subnet"].ToString();
                var ipNetwork = ipStr.Contains("/") ? IPNetwork.Parse(ipStr) : IPNetwork.Parse(ipStr, 24);
                dnsQMsg.EDnsOptions.Options.Add(new ClientSubnetOption(ipNetwork.Cidr, ipNetwork.Network));
            }
            else
                dnsQMsg.EDnsOptions.Options.Add(
                    new ClientSubnetOption(24, IPNetwork.Parse(RealIP.Get(context), 24).Network));
            return dnsQMsg;
        }

        public static DnsMessage FromWebBase64(string base64) => DnsMessage.Parse(DecodeWebBase64(base64));
    }
}
