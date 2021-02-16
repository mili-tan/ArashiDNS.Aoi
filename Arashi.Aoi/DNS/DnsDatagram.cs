using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using ArDns = ARSoft.Tools.Net.Dns;

namespace Arashi.Azure
{
    public sealed class DnsDatagram
    {
        ushort ID = 0;
        byte QR;
        byte OPCODE;
        byte AA = 0;
        byte TC;
        byte RD;
        byte RA;
        byte MZ = 0;
        byte AD;
        byte CD;
        byte RCODE;

        IReadOnlyList<DnsRecords.QuestionItem> Question;
        IReadOnlyList<DnsRecords.RecordItem> Answer;
        IReadOnlyList<DnsRecords.RecordItem> Authority;
        IReadOnlyList<DnsRecords.RecordItem> Additional;

        private DnsDatagram() { }

        public static DnsDatagram ReadFromJson(dynamic jsonResponse)
        {
            var data = new DnsDatagram
            {
                QR = 1, //Is Response
                OPCODE = 0, //StandardQuery
                TC = (byte) (jsonResponse.TC.Value ? 1 : 0),
                RD = (byte) (jsonResponse.RD.Value ? 1 : 0),
                RA = (byte) (jsonResponse.RA.Value ? 1 : 0),
                AD = (byte) (jsonResponse.AD.Value ? 1 : 0),
                CD = (byte) (jsonResponse.CD.Value ? 1 : 0),
                RCODE = (byte) jsonResponse.Status
            };

            if (jsonResponse.Question == null)
                data.Question = Array.Empty<DnsRecords.QuestionItem>();
            else
            {
                var question = new List<DnsRecords.QuestionItem>(Convert.ToUInt16(jsonResponse.Question.Count));
                data.Question = question;
                foreach (var jsonQuestionRecord in jsonResponse.Question)
                    question.Add(new DnsRecords.QuestionItem(jsonQuestionRecord));
            }

            if (jsonResponse.Answer == null)
                data.Answer = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var answer = new List<DnsRecords.RecordItem>(Convert.ToUInt16(jsonResponse.Answer.Count));
                data.Answer = answer;
                foreach (var jsonAnswerRecord in jsonResponse.Answer)
                    answer.Add(new DnsRecords.RecordItem(jsonAnswerRecord));
            }

            if (jsonResponse.Authority == null)
                data.Authority = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var authority = new List<DnsRecords.RecordItem>(Convert.ToUInt16(jsonResponse.Authority.Count));
                data.Authority = authority;
                foreach (var jsonAuthorityRecord in jsonResponse.Authority)
                    authority.Add(new DnsRecords.RecordItem(jsonAuthorityRecord));
            }

            if (jsonResponse.Additional == null)
                data.Additional = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var additional = new List<DnsRecords.RecordItem>(Convert.ToUInt16(jsonResponse.Additional.Count));
                data.Additional = additional;
                foreach (var jsonAdditionalRecord in jsonResponse.Additional)
                    additional.Add(new DnsRecords.RecordItem(jsonAdditionalRecord));
            }

            return data;
        }

        public static DnsDatagram ReadFromDnsMessage(DnsMessage dnsResponse)
        {
            dnsResponse.IsEDnsEnabled = false;
            dnsResponse.EDnsOptions = null;
            dnsResponse.TSigOptions = null;

            var data = new DnsDatagram
            {
                QR = 1,
                OPCODE = 0,
                TC = (byte) (dnsResponse.IsTruncated ? 1 : 0),
                RD = (byte) (dnsResponse.IsRecursionDesired ? 1 : 0),
                RA = (byte) (dnsResponse.IsRecursionAllowed ? 1 : 0),
                AD = (byte) (dnsResponse.IsAuthenticData ? 1 : 0),
                CD = (byte) (dnsResponse.IsCheckingDisabled ? 1 : 0),
                RCODE = (byte) dnsResponse.ReturnCode
            };

            if (dnsResponse.Questions == null)
                data.Question = Array.Empty<DnsRecords.QuestionItem>();
            else
            {
                var question = new List<DnsRecords.QuestionItem>(Convert.ToUInt16(dnsResponse.Questions.Count));
                data.Question = question;
                question.AddRange(from dynamic jsonQuestionRecord in dnsResponse.Questions
                    select new DnsRecords.QuestionItem(jsonQuestionRecord));
            }

            if (dnsResponse.AnswerRecords == null)
                data.Answer = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var answer = new List<DnsRecords.RecordItem>(Convert.ToUInt16(dnsResponse.AnswerRecords.Count));
                data.Answer = answer;
                answer.AddRange(from jsonAnswerRecord in dnsResponse.AnswerRecords
                    where !jsonAnswerRecord.Name.IsSubDomainOf(DomainName.Parse("arashi-msg")) &&
                          !jsonAnswerRecord.Name.IsSubDomainOf(DomainName.Parse("nova-msg")) ||
                          jsonAnswerRecord.RecordType != RecordType.Txt
                    select new DnsRecords.RecordItem(jsonAnswerRecord));
            }

            if (dnsResponse.AuthorityRecords == null)
                data.Authority = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var authority = new List<DnsRecords.RecordItem>(Convert.ToUInt16(dnsResponse.AuthorityRecords.Count));
                data.Authority = authority;
                authority.AddRange(dnsResponse.AuthorityRecords.Select(jsonAuthorityRecord =>
                    new DnsRecords.RecordItem(jsonAuthorityRecord)));
            }

            if (dnsResponse.AdditionalRecords == null)
                data.Additional = Array.Empty<DnsRecords.RecordItem>();
            else
            {
                var additional = new List<DnsRecords.RecordItem>(Convert.ToUInt16(dnsResponse.AdditionalRecords.Count));
                data.Additional = additional;
                additional.AddRange(dnsResponse.AdditionalRecords.Select(jsonAdditionalRecord =>
                    new DnsRecords.RecordItem(jsonAdditionalRecord)));
            }

            return data;
        }

        internal static void WriteUInt16NetworkOrder(ushort value, Stream s)
        {
            var b = BitConverter.GetBytes(value);
            Array.Reverse(b);
            s.Write(b, 0, 2);
        }

        internal static void WriteUInt32NetworkOrder(uint value, Stream s)
        {
            var b = BitConverter.GetBytes(value);
            Array.Reverse(b);
            s.Write(b, 0, 4);
        }

        public static void SerializeDomainName(string domain, Stream s, List<DnsDomainOffset> domainEntries = null)
        {
            while (!string.IsNullOrEmpty(domain))
            {
                if (domainEntries != null)
                {
                    foreach (var domainEntry in domainEntries)
                    {
                        if (!domain.Equals(domainEntry.Domain, StringComparison.OrdinalIgnoreCase)) continue;
                        ushort pointer = 0xC000;
                        pointer |= domainEntry.Offset;
                        var pointerBytes = BitConverter.GetBytes(pointer);
                        Array.Reverse(pointerBytes);
                        s.Write(pointerBytes, 0, 2);
                        return;
                    }

                    domainEntries.Add(new DnsDomainOffset(Convert.ToUInt16(s.Position), domain));
                }

                string label;
                var i = domain.IndexOf('.');
                if (i < 0)
                {
                    label = domain;
                    domain = null;
                }
                else
                {
                    label = domain.Substring(0, i);
                    domain = domain.Substring(i + 1);
                }

                var labelBytes = Encoding.ASCII.GetBytes(label);
                if (labelBytes.Length > 63)
                    throw new Exception("ConvertDomainToLabel: Invalid domain name. Label cannot exceed 63 bytes.");

                s.WriteByte(Convert.ToByte(labelBytes.Length));
                s.Write(labelBytes, 0, labelBytes.Length);
            }

            s.WriteByte(Convert.ToByte(0));
        }

        internal static string DecodeCharacterString(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\"")) value = value[1..^1].Replace("\\\"", "\"");
            return value;
        }

        public void WriteToUdp(Stream s)
        {
            WriteHeaders(s, Question.Count, Answer.Count, Authority.Count, Additional.Count);

            var domainEntries = new List<DnsDomainOffset>(1);
            foreach (var t in Question) t.WriteTo(s, domainEntries);
            foreach (var t in Answer) t.WriteTo(s, domainEntries);
            foreach (var t in Authority) t.WriteTo(s, domainEntries);
            foreach (var t in Additional) t.WriteTo(s, domainEntries);
        }

        private void WriteHeaders(Stream s, int qdCount, int anCount, int nsCount, int arCount)
        {
            WriteUInt16NetworkOrder(ID, s);
            s.WriteByte(Convert.ToByte((QR << 7) | (OPCODE << 3) | (AA << 2) | (TC << 1) | RD));
            s.WriteByte(Convert.ToByte((RA << 7) | (MZ << 6) | (AD << 5) | (CD << 4) | RCODE));
            WriteUInt16NetworkOrder(Convert.ToUInt16(qdCount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(anCount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(nsCount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(arCount), s);
        }

        public class DnsDomainOffset
        {
            public DnsDomainOffset(ushort offset, string domain)
            {
                Offset = offset;
                Domain = domain;
            }

            public ushort Offset { get; }
            public string Domain { get; }
        }
    }

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
