using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using ARSoft.Tools.Net.Dns;
using ArDns = ARSoft.Tools.Net.Dns;

namespace Arashi.Azure
{
    public class DnsRecords
    {
        public sealed class RecordItem
        {
            readonly string Name;
            readonly ushort Class;
            readonly uint Ttl;
            readonly RecordData Data;
            readonly RecordType Type;

            public RecordItem(DnsRecordBase dnsRecord)
            {
                Name = dnsRecord.Name.ToString().TrimEnd('.');
                Type = dnsRecord.RecordType;
                Class = 1; //DnsClass.IN Internet 
                Ttl = Convert.ToUInt16(dnsRecord.TimeToLive);
                //Type = Enum.TryParse(jsonRecord.type, out RecordType type) ? type : (RecordType) jsonRecord.type;

                Data = Type switch
                {
                    RecordType.A => new ARecord(dnsRecord),
                    RecordType.Ns => new NsRecord(dnsRecord),
                    RecordType.CName => new CNameRecord(dnsRecord),
                    RecordType.Ptr => new PtrRecord(dnsRecord),
                    RecordType.Mx => new MxRecord(dnsRecord),
                    RecordType.Txt => new TxtRecord(dnsRecord),
                    RecordType.Aaaa => new AaaaRecord(dnsRecord),
                    _ => new UnknownRecord(dnsRecord)
                };
            }

            public RecordItem(dynamic jsonRecord)
            {
                Name = (jsonRecord.name.Value as string).TrimEnd('.');
                Type = (RecordType)jsonRecord.type;
                Class = 1; //DnsClass.IN Internet 
                Ttl = jsonRecord.TTL;
                //Type = Enum.TryParse(jsonRecord.type, out RecordType type) ? type : (RecordType) jsonRecord.type;

                Data = Type switch
                {
                    RecordType.A => new ARecord(jsonRecord),
                    RecordType.Ns => new NsRecord(jsonRecord),
                    RecordType.CName => new CNameRecord(jsonRecord),
                    RecordType.Ptr => new PtrRecord(jsonRecord),
                    RecordType.Mx => new MxRecord(jsonRecord),
                    RecordType.Txt => new TxtRecord(jsonRecord),
                    RecordType.Aaaa => new AaaaRecord(jsonRecord),
                    _ => new UnknownRecord(jsonRecord)
                };
            }

            public void WriteTo(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries)
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
            protected abstract void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries);

            public void WriteTo(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries)
            {
                var originalPosition = s.Position;
                s.Write(new byte[] { 0, 0 }, 0, 2);
                WriteRecordData(s, domainEntries); //RDATA
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

            public QuestionItem(DnsQuestion questionRecord)
            {
                Name = questionRecord.Name.ToString().TrimEnd('.');
                Type = questionRecord.RecordType;
                Class = 1;
            }

            public void WriteTo(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries)
            {
                DnsDatagram.SerializeDomainName(Name, s, domainEntries);
                DnsDatagram.WriteUInt16NetworkOrder((ushort)Type, s);
                DnsDatagram.WriteUInt16NetworkOrder(Class, s);
            }
        }

        public class AaaaRecord : RecordData
        {
            IPAddress Address;

            public AaaaRecord(DnsRecordBase dnsRecord) => Address = (dnsRecord as ArDns.AaaaRecord).Address;

            public AaaaRecord(dynamic jsonRecord) => Address = IPAddress.Parse(jsonRecord.data.Value);

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                s.Write(Address.GetAddressBytes());
        }

        public class ARecord : RecordData
        {
            IPAddress Address;

            public ARecord(DnsRecordBase dnsRecord) => Address = (dnsRecord as ArDns.ARecord).Address;

            public ARecord(dynamic jsonRecord) => Address = IPAddress.Parse(jsonRecord.data.Value);

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                s.Write(Address.GetAddressBytes());
        }

        public class CNameRecord : RecordData
        {
            string Domain;

            public CNameRecord(DnsRecordBase dnsRecord) =>
                Domain = (dnsRecord as ArDns.CNameRecord).CanonicalName.ToString().TrimEnd('.');

            public CNameRecord(dynamic jsonRecord) => Domain = (jsonRecord.data.Value as string).TrimEnd('.');

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
        }

        public class MxRecord : RecordData
        {
            ushort Preference;
            string Exchange;

            public MxRecord(DnsRecordBase dnsRecord)
            {
                var dns = dnsRecord as ArDns.MxRecord;
                Preference = dns.Preference;
                Exchange = dns.ExchangeDomainName.ToString().TrimEnd('.');
            }

            public MxRecord(dynamic jsonRecord)
            {
                var parts = (jsonRecord.data.Value as string).Split(' ');
                Preference = ushort.Parse(parts[0]);
                Exchange = parts[1].TrimEnd('.');
            }

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries)
            {
                DnsDatagram.WriteUInt16NetworkOrder(Preference, s);
                DnsDatagram.SerializeDomainName(Exchange, s, domainEntries);
            }
        }

        public class NsRecord : RecordData
        {
            string NameServer;

            public NsRecord(dynamic dnsRecord) => NameServer = (dnsRecord.data.Value as string).TrimEnd('.');

            public NsRecord(DnsRecordBase dnsRecord) =>
                NameServer = (dnsRecord as ArDns.NsRecord).NameServer.ToString().TrimEnd('.');

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                DnsDatagram.SerializeDomainName(NameServer, s, domainEntries);
        }

        public class PtrRecord : RecordData
        {
            string Domain;

            public PtrRecord(DnsRecordBase dnsRecord) =>
                Domain = (dnsRecord as ArDns.PtrRecord).PointerDomainName.ToString().TrimEnd('.');

            public PtrRecord(dynamic jsonRecord) => Domain = (jsonRecord.data.Value as string).TrimEnd('.');

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                DnsDatagram.SerializeDomainName(Domain, s, domainEntries);
        }

        public class TxtRecord : RecordData
        {
            string Text;

            public TxtRecord(DnsRecordBase dnsRecord) =>
                Text = DnsDatagram.DecodeCharacterString((dnsRecord as ArDns.TxtRecord).TextData);

            public TxtRecord(dynamic jsonRecord) => Text = DnsDatagram.DecodeCharacterString(jsonRecord.data.Value);

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries)
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
                } while (offset < data.Length);
            }
        }

        public class UnknownRecord : RecordData
        {
            byte[] Data;

            public UnknownRecord(DnsRecordBase dnsRecord)
            {
                try
                {
                    Data = (dnsRecord as ArDns.UnknownRecord).RecordData;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Data = new byte[0];
                }
            }

            public UnknownRecord(dynamic jsonRecord) => Data = Encoding.ASCII.GetBytes(jsonRecord.data.Value as string);

            protected override void WriteRecordData(Stream s, List<DnsDatagram.DnsDomainOffset> domainEntries) =>
                s.Write(Data, 0, Data.Length);
        }
    }
}
