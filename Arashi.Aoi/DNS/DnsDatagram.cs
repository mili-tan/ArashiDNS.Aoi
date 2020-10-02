using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        IReadOnlyList<QuestionItem> Question;
        IReadOnlyList<RecordItem> Answer;
        IReadOnlyList<RecordItem> Authority;
        IReadOnlyList<RecordItem> Additional;

        private DnsDatagram() { }

        public static DnsDatagram ReadFromJson(dynamic jsonResponse)
        {
            DnsDatagram datagram = new DnsDatagram
            {
                QR = 1,//Is Response
                OPCODE = 0,//StandardQuery
                TC = (byte) (jsonResponse.TC.Value ? 1 : 0),
                RD = (byte) (jsonResponse.RD.Value ? 1 : 0),
                RA = (byte) (jsonResponse.RA.Value ? 1 : 0),
                AD = (byte) (jsonResponse.AD.Value ? 1 : 0),
                CD = (byte) (jsonResponse.CD.Value ? 1 : 0),
                RCODE = (byte) jsonResponse.Status
            };

            if (jsonResponse.Question == null)
                datagram.Question = Array.Empty<QuestionItem>();
            else
            {
                var question = new List<QuestionItem>(Convert.ToUInt16(jsonResponse.Question.Count));
                datagram.Question = question;
                foreach (dynamic jsonQuestionRecord in jsonResponse.Question)
                    question.Add(new QuestionItem(jsonQuestionRecord));
            }

            if (jsonResponse.Answer == null)
                datagram.Answer = Array.Empty<RecordItem>();
            else
            {
                var answer = new List<RecordItem>(Convert.ToUInt16(jsonResponse.Answer.Count));
                datagram.Answer = answer;
                foreach (var jsonAnswerRecord in jsonResponse.Answer)
                    answer.Add(new RecordItem(jsonAnswerRecord));
            }

            if (jsonResponse.Authority == null)
                datagram.Authority = Array.Empty<RecordItem>();
            else
            {
                var authority = new List<RecordItem>(Convert.ToUInt16(jsonResponse.Authority.Count));
                datagram.Authority = authority;
                foreach (var jsonAuthorityRecord in jsonResponse.Authority)
                    authority.Add(new RecordItem(jsonAuthorityRecord));
            }

            if (jsonResponse.Additional == null)
                datagram.Additional = Array.Empty<RecordItem>();
            else
            {
                var additional = new List<RecordItem>(Convert.ToUInt16(jsonResponse.Additional.Count));
                datagram.Additional = additional;
                foreach (var jsonAdditionalRecord in jsonResponse.Additional)
                    additional.Add(new RecordItem(jsonAdditionalRecord));
            }

            return datagram;
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

        private void WriteHeaders(Stream s, int qdcount, int ancount, int nscount, int arcount)
        {
            WriteUInt16NetworkOrder(ID, s);
            s.WriteByte(Convert.ToByte((QR << 7) | (OPCODE << 3) | (AA << 2) | (TC << 1) | RD));
            s.WriteByte(Convert.ToByte((RA << 7) | (MZ << 6) | (AD << 5) | (CD << 4) | RCODE));
            WriteUInt16NetworkOrder(Convert.ToUInt16(qdcount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(ancount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(nscount), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(arcount), s);
        }
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
