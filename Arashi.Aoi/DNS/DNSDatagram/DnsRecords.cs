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

        public RecordItem(dynamic jsonResourceRecord)
        {
            Name = (jsonResourceRecord.name.Value as string).TrimEnd('.');
            Type = (RecordType)jsonResourceRecord.type;
            Class = 1;//DnsClass.IN Internet 
            Ttl = jsonResourceRecord.TTL;

            Data = Type switch
            {
                RecordType.A => new ARecord(jsonResourceRecord),
                RecordType.Ns => new NsRecord(jsonResourceRecord),
                RecordType.CName => new CnameRecord(jsonResourceRecord),
                RecordType.Ptr => new PtrRecord(jsonResourceRecord),
                RecordType.Mx => new MxRecord(jsonResourceRecord),
                RecordType.Txt => new TxtRecord(jsonResourceRecord),
                RecordType.Aaaa => new DnsAaaaRecord(jsonResourceRecord),
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

        public UnknownRecord(dynamic jsonResourceRecord)
        {
            Length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            Data = Encoding.ASCII.GetBytes(jsonResourceRecord.data.Value as string);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries) => s.Write(Data, 0, Data.Length);
    }
}
