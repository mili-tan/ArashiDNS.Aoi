using System;
using System.Collections.Generic;
using System.Linq;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public static class DnsEncoder {

        public static byte[] Encode(DnsMessage dnsMsg, bool transIdEnable = false, bool trimEnable = true,
            ushort id = 0)
        {
            dnsMsg.IsRecursionAllowed = true;
            dnsMsg.IsRecursionDesired = true;
            dnsMsg.IsQuery = false;
            dnsMsg.IsEDnsEnabled = false;
            dnsMsg.AdditionalRecords?.Clear();
            dnsMsg.EDnsOptions?.Options?.Clear();

            if (id != 0) dnsMsg.TransactionID = id;
            if (!transIdEnable) dnsMsg.TransactionID = 0;
            //if (dnsMsg.ReturnCode != ReturnCode.NoError) dnsMsg.TransactionID = 0;

            dnsMsg.AdditionalRecords.RemoveAll(item =>
                item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) ||
                item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

            //if (dnsBytes != null && dnsBytes[2] == 0) dnsBytes[2] = 1;
            dnsMsg.Encode(false, out var dnsBytes);
            return trimEnable ? bytesTrimEnd(dnsBytes) : dnsBytes;
        }

        private static byte[] bytesTrimEnd(byte[] bytes, bool appendZero = false)
        {
            var list = bytes.ToList();
            var count = 0;
            for (var i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] == 0x00)
                {
                    list.RemoveAt(i);
                    count++;
                }
                else
                    break;
            }

            if (count % 2 == 1 && appendZero) list.AddRange(new byte[] {0x00});
            return list.ToArray();
        }

        public static byte[] EncodeQuery(DnsMessage dnsQMsg)
        {
            dnsQMsg.IsRecursionAllowed = true;
            dnsQMsg.IsRecursionDesired = true;
            dnsQMsg.TransactionID = Convert.ToUInt16(new Random(DateTime.Now.Millisecond).Next(1, 10));
            dnsQMsg.Encode(false, out var dnsBytes);
            return dnsBytes;
        }
    }
}
