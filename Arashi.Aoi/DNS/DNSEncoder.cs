using System;
using System.Security.Cryptography;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public static class DnsEncoder
    {
        public static byte[] Encode(DnsMessage dnsMsg, bool transIdEnable = false, bool trimEnable = false,
            ushort id = 0, bool keepEcs = false)
        {
            dnsMsg.IsRecursionAllowed = true;
            dnsMsg.IsRecursionDesired = true;
            dnsMsg.IsQuery = false;

            if (keepEcs)
            {
                dnsMsg.EDnsOptions?.Options?.RemoveAll(x => x.Type != EDnsOptionType.ClientSubnet);
                dnsMsg.IsEDnsEnabled = dnsMsg.EDnsOptions?.Options?.Count > 0;
            }
            else
            {
                dnsMsg.IsEDnsEnabled = false;
                dnsMsg.EDnsOptions?.Options?.Clear();
                dnsMsg.AdditionalRecords?.Clear();
            }

            dnsMsg.TransactionID = id;
            if (!transIdEnable) dnsMsg.TransactionID = 0;

            dnsMsg.AuthorityRecords.RemoveAll(item =>
                item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) ||
                item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

            var dnsBytes = dnsMsg.Encode().ToArraySegment(false).ToArray();
            return trimEnable ? DnsMessageTrimHelper.TrimDnsMessage(dnsBytes) : dnsBytes;
        }

        public static byte[] EncodeQuery(DnsMessage dnsQMsg, bool trimEnable = false)
        {
            dnsQMsg.IsRecursionAllowed = true;
            dnsQMsg.IsRecursionDesired = true;

            if (dnsQMsg.TransactionID == 0)
            {
                var buf = new byte[2];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(buf);

                ushort newId = (ushort)((buf[0] << 8) | buf[1]);
                dnsQMsg.TransactionID = newId == 0 ? (ushort)1 : newId;
            }

            var dnsBytes = dnsQMsg.Encode().ToArraySegment(false).ToArray();
            return trimEnable ? DnsMessageTrimHelper.TrimDnsMessage(dnsBytes) : dnsBytes;
        }
    }

    public static class DnsMessageTrimHelper
    {
        public static byte[] TrimDnsMessage(byte[] bytes, bool appendZero = false)
        {
            try
            {
                if (bytes == null || bytes.Length == 0)
                    return bytes ?? Array.Empty<byte>();

                int actualLength;
                try
                {
                    actualLength = GetDnsMessageLength(bytes);
                    if (actualLength < 0 || actualLength > bytes.Length)
                        return bytes;
                }
                catch
                {
                    return bytes;
                }

                int padding = bytes.Length - actualLength;
                int finalLength = (appendZero && (padding % 2 == 1)) ? actualLength + 1 : actualLength;

                if (finalLength == bytes.Length)
                    return bytes;

                byte[] result = new byte[finalLength];
                Array.Copy(bytes, 0, result, 0, actualLength);
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return bytes;
            }
        }

        private static int GetDnsMessageLength(byte[] msg)
        {
            if (msg.Length < 12)
                throw new IndexOutOfRangeException();

            ushort qdcount = (ushort)((msg[4] << 8) | msg[5]);
            ushort ancount = (ushort)((msg[6] << 8) | msg[7]);
            ushort nscount = (ushort)((msg[8] << 8) | msg[9]);
            ushort arcount = (ushort)((msg[10] << 8) | msg[11]);

            int offset = 12;

            for (int i = 0; i < qdcount; i++)
            {
                offset = SkipName(msg, offset);
                if (offset + 4 > msg.Length) throw new IndexOutOfRangeException();
                offset += 4; // QTYPE + QCLASS
            }

            int totalRR = ancount + nscount + arcount;
            for (int i = 0; i < totalRR; i++)
            {
                offset = SkipName(msg, offset);

                if (offset + 10 > msg.Length) throw new IndexOutOfRangeException();
                ushort rdlen = (ushort)((msg[offset + 8] << 8) | msg[offset + 9]);
                offset += 10; // TYPE(2)+CLASS(2)+TTL(4)+RDLENGTH(2)

                if (offset + rdlen > msg.Length) throw new IndexOutOfRangeException();
                offset += rdlen; // RDATA
            }

            return offset;
        }

        private static int SkipName(byte[] msg, int offset)
        {
            while (true)
            {
                if (offset >= msg.Length) throw new IndexOutOfRangeException();

                byte b = msg[offset];

                if ((b & 0xC0) == 0xC0)
                {
                    if (offset + 2 > msg.Length) throw new IndexOutOfRangeException();
                    return offset + 2;
                }

                if (b == 0)
                    return offset + 1;

                if ((b & 0xC0) != 0)
                    throw new IndexOutOfRangeException();

                offset += 1 + b;
            }
        }
    }
}