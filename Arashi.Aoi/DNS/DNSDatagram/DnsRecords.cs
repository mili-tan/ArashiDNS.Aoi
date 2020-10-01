using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Arashi.Azure
{
    public enum RecordType : ushort
    {
        A = 1,
        NS = 2,
        MD = 3,
        MF = 4,
        CNAME = 5,
        SOA = 6,
        MB = 7,
        MG = 8,
        MR = 9,
        NULL = 10,
        WKS = 11,
        PTR = 12,
        HINFO = 13,
        MINFO = 14,
        MX = 15,
        TXT = 16,
        RP = 17,
        AFSDB = 18,
        X25 = 19,
        ISDN = 20,
        RT = 21,
        NSAP = 22,
        NSAP_PTR = 23,
        SIG = 24,
        KEY = 25,
        PX = 26,
        GPOS = 27,
        AAAA = 28,
        LOC = 29,
        NXT = 30,
        EID = 31,
        NIMLOC = 32,
        SRV = 33,
        ATMA = 34,
        NAPTR = 35,
        KX = 36,
        CERT = 37,
        A6 = 38,
        DNAME = 39,
        SINK = 40,
        OPT = 41,
        APL = 42,
        DS = 43,
        SSHFP = 44,
        IPSECKEY = 45,
        RRSIG = 46,
        NSEC = 47,
        DNSKEY = 48,
        DHCID = 49,
        NSEC3 = 50,
        NSEC3PARAM = 51,
        TLSA = 52,
        SMIMEA = 53,
        HIP = 55,
        NINFO = 56,
        RKEY = 57,
        TALINK = 58,
        CDS = 59,
        CDNSKEY = 60,
        OPENPGPKEY = 61,
        CSYNC = 62,
        SPF = 99,
        UINFO = 100,
        UID = 101,
        GID = 102,
        UNSPEC = 103,
        NID = 104,
        L32 = 105,
        L64 = 106,
        LP = 107,
        EUI48 = 108,
        EUI64 = 109,
        TKEY = 249,
        TSIG = 250,
        IXFR = 251,
        AXFR = 252,
        MAILB = 253,
        MAILA = 254,
        ANY = 255,
        URI = 256,
        CAA = 257,
        AVC = 258,
        TA = 32768,
        DLV = 32769,
        ANAME = 65280
    }

    public sealed class RecordItem
    {
        readonly string Name;
        readonly ushort Class;
        readonly uint Ttl;
        readonly RecordData Data;
        readonly RecordType Type;

        public RecordItem(dynamic jsonResourceRecord)
        {
            Name = (jsonResourceRecord.name.Value as string).TrimEnd('.');
            Type = (RecordType)jsonResourceRecord.type;
            Class = 1;//DnsClass.IN Internet 
            Ttl = jsonResourceRecord.TTL;

            Data = Type switch
            {
                RecordType.A => new ARecord(jsonResourceRecord),
                RecordType.NS => new NsRecord(jsonResourceRecord),
                RecordType.CNAME => new CnameRecord(jsonResourceRecord),
                RecordType.PTR => new PtrRecord(jsonResourceRecord),
                RecordType.MX => new MxRecord(jsonResourceRecord),
                RecordType.TXT => new TxtRecord(jsonResourceRecord),
                RecordType.AAAA => new DnsAaaaRecord(jsonResourceRecord),
                _ => new UnknownRecord(jsonResourceRecord)
            };
        }

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.SerializeDomainName(Name, s, domainEntries);
            DnsDatagram.WriteUInt16NetworkOrder((ushort)Type, s);
            DnsDatagram.WriteUInt16NetworkOrder(Class, s);
            DnsDatagram.WriteUInt32NetworkOrder(Ttl, s);
            Data.WriteTo(s, domainEntries);
        }
    }
    public abstract class RecordData
    {
        protected ushort Length;
        protected abstract void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries);

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            var originalPosition = s.Position;
            s.Write(new byte[] { 0, 0 }, 0, 2);
            WriteRecordData(s, domainEntries);//RDATA
            var finalPosition = s.Position;
            var length = Convert.ToUInt16(finalPosition - originalPosition - 2);
            s.Position = originalPosition;
            DnsDatagram.WriteUInt16NetworkOrder(length, s);
            s.Position = finalPosition;
        }
    }
    public class QuestionItem
    {
        readonly string Name;
        readonly RecordType Type;
        readonly ushort Class;

        public QuestionItem(dynamic jsonQuestionRecord)
        {
            Name = (jsonQuestionRecord.name.Value as string).TrimEnd('.');
            Type = (RecordType)jsonQuestionRecord.type;
            Class = 1;
        }

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.SerializeDomainName(Name, s, domainEntries);
            DnsDatagram.WriteUInt16NetworkOrder((ushort)Type, s);
            DnsDatagram.WriteUInt16NetworkOrder(Class, s);
        }
    }
    public class DnsAaaaRecord : RecordData
    {
        IPAddress Address;

        public DnsAaaaRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Address = IPAddress.Parse(jsonResourceRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            s.Write(Address.GetAddressBytes());
    }

    public class ARecord : RecordData
    {
        IPAddress Address;

        public ARecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Address = IPAddress.Parse(jsonResourceRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            s.Write(Address.GetAddressBytes());
    }

    public class CnameRecord : RecordData
    {
        string Domain;

        public CnameRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Domain = (jsonResourceRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
    }

    public class MxRecord : RecordData
    {
        ushort Preference;
        string Exchange;

        public MxRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            var parts = (jsonResourceRecord.data.Value as string).Split(' ');
            Preference = ushort.Parse(parts[0]);
            Exchange = parts[1].TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.WriteUInt16NetworkOrder(Preference, s);
            DnsDatagram.SerializeDomainName(Exchange, s, domainEntries);
        }
    }

    public class NsRecord : RecordData
    {
        string NameServer;

        public NsRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            NameServer = (jsonResourceRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            DnsDatagram.SerializeDomainName(NameServer, s, domainEntries);
    }

    public class PtrRecord : RecordData
    {
        string Domain;

        public PtrRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Domain = (jsonResourceRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) => DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
    }

    public class TxtRecord : RecordData
    {
        string Text;

        public TxtRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Text = DnsDatagram.DecodeCharacterString(jsonResourceRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            var data = Encoding.ASCII.GetBytes(Text);
            var offset = 0;
            do
            {
                var length = data.Length - offset;
                if (length > 255)
                    length = 255;

                s.WriteByte(Convert.ToByte(length));
                s.Write(data, offset, length);

                offset += length;
            }
            while (offset < data.Length);
        }
    }

    public class UnknownRecord : RecordData
    {
        byte[] Data;

        public UnknownRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Data = Encoding.ASCII.GetBytes(jsonResourceRecord.data.Value as string);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) => s.Write(Data, 0, Data.Length);
    }
}
