using System;
using System.Linq;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace Arashi
{
    public static class DnsEncoder
    {

        public static byte[] Encode(DnsMessage dnsMsg, bool transIdEnable = false, bool trimEnable = false,
            ushort id = 0)
        {
            dnsMsg.IsRecursionAllowed = true;
            dnsMsg.IsRecursionDesired = true;
            dnsMsg.IsQuery = false;
            dnsMsg.IsEDnsEnabled = false;
            dnsMsg.EDnsOptions?.Options?.Clear();
            dnsMsg.AdditionalRecords?.Clear();

            if (id != 0) dnsMsg.TransactionID = id;
            if (!transIdEnable) dnsMsg.TransactionID = 0;
            //if (dnsMsg.ReturnCode != ReturnCode.NoError) dnsMsg.TransactionID = 0;

            dnsMsg.AuthorityRecords.RemoveAll(item =>
                item.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) ||
                item.Name.IsSubDomainOf(DomainName.Parse("nova-msg")));

            //if (dnsBytes != null && dnsBytes[2] == 0) dnsBytes[2] = 1;
            var dnsBytes = dnsMsg.Encode().ToArraySegment(false).ToArray();
            return trimEnable ? DnsMessageTrimHelper.TrimDnsMessage(dnsBytes) : dnsBytes;
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
            var dnsBytes = dnsQMsg.Encode().ToArraySegment(false).ToArray();
            return dnsBytes;
        }
    }

    public static class DnsMessageTrimHelper
    {
        public static byte[] TrimDnsMessage(byte[] bytes, bool appendZero = false)
        {
            if (bytes == null || bytes.Length == 0)
                return bytes ?? Array.Empty<byte>();

            int actualLength;
            try
            {
                actualLength = GetDnsMessageLength(bytes);

                if (actualLength > bytes.Length)
                {
                    throw new IndexOutOfRangeException("Parsed DNS message length exceeds actual byte array length.");
                }
            }
            catch
            {
                return SimpleTrimEnd(bytes, appendZero);
            }

            int padding = bytes.Length - actualLength;
            int finalLength = (appendZero && (padding % 2 == 1)) ? actualLength + 1 : actualLength;

            if (finalLength == bytes.Length)
                return bytes;

            byte[] result = new byte[finalLength];
            Array.Copy(bytes, 0, result, 0, Math.Min(actualLength, finalLength));


            return result;
        }

        private static int GetDnsMessageLength(byte[] msg)
        {
            int offset = 0;
            if (offset + 12 > msg.Length) throw new IndexOutOfRangeException();

            ushort qdcount = (ushort) ((msg[4] << 8) | msg[5]);
            ushort ancount = (ushort) ((msg[6] << 8) | msg[7]);
            ushort nscount = (ushort) ((msg[8] << 8) | msg[9]);
            ushort arcount = (ushort) ((msg[10] << 8) | msg[11]);
            offset = 12;

            for (int i = 0; i < qdcount; i++)
            {
                offset = SkipName(msg, offset);
                offset += 4; // QTYPE + QCLASS 
                if (offset > msg.Length) throw new IndexOutOfRangeException();
            }

            int totalRR = ancount + nscount + arcount;
            for (int i = 0; i < totalRR; i++)
            {
                offset = SkipName(msg, offset);

                if (offset + 10 > msg.Length) throw new IndexOutOfRangeException();

                ushort rdlen = (ushort) ((msg[offset + 8] << 8) | msg[offset + 9]);
                offset += 10 + rdlen; // TYPE(2)+CLASS(2)+TTL(4)+RDLEN(2) + RDATA

                if (offset > msg.Length) throw new IndexOutOfRangeException();
            }

            return offset;
        }

        private static int SkipName(byte[] msg, int offset)
        {
            while (true)
            {
                if (offset >= msg.Length) throw new IndexOutOfRangeException();

                byte b = msg[offset];
                if ((b & 0xC0) == 0xC0) // 压缩指针
                {
                    if (offset + 2 > msg.Length) throw new IndexOutOfRangeException();
                    return offset + 2;
                }

                if (b == 0) // Name 结束符
                {
                    return offset + 1;
                }

                // 跳过当前 Label (长度字节 1 + 实际字符长度)
                offset += 1 + (b & 0x3F);
            }
        }

        private static byte[] SimpleTrimEnd(byte[] bytes, bool appendZero)
        {
            int count = 0;
            for (int i = bytes.Length - 1; i >= 0 && bytes[i] == 0; i--)
            {
                count++;
            }

            if (count == 0 || (count == 1 && appendZero))
                return bytes;

            int finalLength = bytes.Length - count;
            if (appendZero && count % 2 == 1)
                finalLength++;

            byte[] result = new byte[finalLength];
            Array.Copy(bytes, 0, result, 0, finalLength);
            return result;
        }
    }
}
