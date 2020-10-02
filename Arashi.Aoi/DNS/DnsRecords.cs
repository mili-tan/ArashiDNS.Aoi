using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using ARSoft.Tools.Net.Dns;

namespace Arashi.Azure
{
    public sealed class RecordItem
    {
        readonly string Name;
        readonly ushort Class;
        readonly uint Ttl;
        readonly RecordData Data;
        readonly RecordType Type;

        public RecordItem(dynamic jsonRecord)
        {
            Name = (jsonRecord.name.Value as string).TrimEnd('.');
            Type = (RecordType) jsonRecord.type;
            Class = 1;//DnsClass.IN Internet 
            Ttl = jsonRecord.TTL;
            //Type = Enum.TryParse(jsonRecord.type, out RecordType type) ? type : (RecordType) jsonRecord.type;

            Data = Type switch
            {
                RecordType.A => new ARecord(jsonRecord),
                RecordType.Ns => new NsRecord(jsonRecord),
                RecordType.CName => new CnameRecord(jsonRecord),
                RecordType.Ptr => new PtrRecord(jsonRecord),
                RecordType.Mx => new MxRecord(jsonRecord),
                RecordType.Txt => new TxtRecord(jsonRecord),
                RecordType.Aaaa => new DnsAaaaRecord(jsonRecord),
                _ => new UnknownRecord(jsonRecord)
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

        public QuestionItem(dynamic jsonRecord)
        {
            Name = (jsonRecord.name.Value as string).TrimEnd('.');
            Type = (RecordType)jsonRecord.type;
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

        public DnsAaaaRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Address = IPAddress.Parse(jsonRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            s.Write(Address.GetAddressBytes());
    }

    public class ARecord : RecordData
    {
        IPAddress Address;

        public ARecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Address = IPAddress.Parse(jsonRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            s.Write(Address.GetAddressBytes());
    }

    public class CnameRecord : RecordData
    {
        string Domain;

        public CnameRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Domain = (jsonRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
    }

    public class MxRecord : RecordData
    {
        ushort Preference;
        string Exchange;

        public MxRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            var parts = (jsonRecord.data.Value as string).Split(' ');
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

        public NsRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            NameServer = (jsonRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) =>
            DnsDatagram.SerializeDomainName(NameServer, s, domainEntries);
    }

    public class PtrRecord : RecordData
    {
        string Domain;

        public PtrRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Domain = (jsonRecord.data.Value as string).TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) => DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
    }

    public class TxtRecord : RecordData
    {
        string Text;

        public TxtRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Text = DnsDatagram.DecodeCharacterString(jsonRecord.data.Value);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            var data = Encoding.ASCII.GetBytes(Text);
            var offset = 0;
            do
            {
                var length = data.Length - offset;
                if (length > 255) length = 255;
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

        public UnknownRecord(dynamic jsonRecord)
        {
            Length = Convert.ToUInt16(jsonRecord.data.Value.Length);
            Data = Encoding.ASCII.GetBytes(jsonRecord.data.Value as string);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) => s.Write(Data, 0, Data.Length);
    }
}
