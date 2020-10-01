/*
Technitium Library
Copyright (C) 2020  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TechnitiumLibrary.IO;

namespace TechnitiumLibrary.Net.Dns
{
    public enum DnsOpcode : byte
    {
        StandardQuery = 0,
        InverseQuery = 1,
        ServerStatusRequest = 2,
        Notify = 4,
        Update = 5
    }

    public enum DnsResponseCode : byte
    {
        NoError = 0,
        FormatError = 1,
        ServerFailure = 2,
        NameError = 3,
        NotImplemented = 4,
        Refused = 5,
        YXDomain = 6,
        YXRRSet = 7,
        NXRRSet = 8,
        NotAuthorized = 9,
        NotZone = 10,
        BADSIG = 16,
        BADKEY = 17,
        BADTIME = 18,
        BADMODE = 19,
        BADNAME = 20,
        BADALG = 21,
        BADTRUNC = 22,
        BADCOOKIE = 23
    }

    public sealed class DnsDatagram
    {
        ushort _ID;

        byte _QR;
        DnsOpcode _OPCODE;
        byte _AA;
        byte _TC;
        byte _RD;
        byte _RA;
        byte _Z;
        byte _AD;
        byte _CD;
        DnsResponseCode _RCODE;

        IReadOnlyList<DnsQuestionRecord> _question;
        IReadOnlyList<DnsResourceRecord> _answer;
        IReadOnlyList<DnsResourceRecord> _authority;
        IReadOnlyList<DnsResourceRecord> _additional;

        private DnsDatagram() { }

        public static DnsDatagram ReadFromJson(dynamic jsonResponse)
        {
            DnsDatagram datagram = new DnsDatagram
            {
                _QR = 1,//is response
                _OPCODE = DnsOpcode.StandardQuery,
                _TC = (byte) (jsonResponse.TC.Value ? 1 : 0),
                _RD = (byte) (jsonResponse.RD.Value ? 1 : 0),
                _RA = (byte) (jsonResponse.RA.Value ? 1 : 0),
                _AD = (byte) (jsonResponse.AD.Value ? 1 : 0),
                _CD = (byte) (jsonResponse.CD.Value ? 1 : 0),
                _RCODE = (DnsResponseCode) jsonResponse.Status
            };


            //question
            if (jsonResponse.Question == null)
            {
                datagram._question = Array.Empty<DnsQuestionRecord>();
            }
            else
            {
                ushort QDCOUNT = Convert.ToUInt16(jsonResponse.Question.Count);
                List<DnsQuestionRecord> question = new List<DnsQuestionRecord>(QDCOUNT);
                datagram._question = question;

                foreach (dynamic jsonQuestionRecord in jsonResponse.Question)
                    question.Add(new DnsQuestionRecord(jsonQuestionRecord));
            }

            //answer
            if (jsonResponse.Answer == null)
            {
                datagram._answer = Array.Empty<DnsResourceRecord>();
            }
            else
            {
                ushort ANCOUNT = Convert.ToUInt16(jsonResponse.Answer.Count);
                List<DnsResourceRecord> answer = new List<DnsResourceRecord>(ANCOUNT);
                datagram._answer = answer;

                foreach (dynamic jsonAnswerRecord in jsonResponse.Answer)
                    answer.Add(new DnsResourceRecord(jsonAnswerRecord));
            }

            //authority
            if (jsonResponse.Authority == null)
            {
                datagram._authority = Array.Empty<DnsResourceRecord>();
            }
            else
            {
                ushort NSCOUNT = Convert.ToUInt16(jsonResponse.Authority.Count);
                List<DnsResourceRecord> authority = new List<DnsResourceRecord>(NSCOUNT);
                datagram._authority = authority;

                foreach (dynamic jsonAuthorityRecord in jsonResponse.Authority)
                    authority.Add(new DnsResourceRecord(jsonAuthorityRecord));
            }

            //additional
            if (jsonResponse.Additional == null)
            {
                datagram._additional = Array.Empty<DnsResourceRecord>();
            }
            else
            {
                ushort ARCOUNT = Convert.ToUInt16(jsonResponse.Additional.Count);
                List<DnsResourceRecord> additional = new List<DnsResourceRecord>(ARCOUNT);
                datagram._additional = additional;

                foreach (dynamic jsonAdditionalRecord in jsonResponse.Additional)
                    additional.Add(new DnsResourceRecord(jsonAdditionalRecord));
            }

            return datagram;
        }

        internal static ushort ReadUInt16NetworkOrder(Stream s)
        {
            byte[] b = s.ReadBytes(2);
            Array.Reverse(b);
            return BitConverter.ToUInt16(b, 0);
        }

        internal static void WriteUInt16NetworkOrder(ushort value, Stream s)
        {
            byte[] b = BitConverter.GetBytes(value);
            Array.Reverse(b);
            s.Write(b, 0, 2);
        }

        internal static void WriteUInt32NetworkOrder(uint value, Stream s)
        {
            byte[] b = BitConverter.GetBytes(value);
            Array.Reverse(b);
            s.Write(b, 0, 4);
        }

        public static void SerializeDomainName(string domain, Stream s, List<DnsDomainOffset> domainEntries = null)
        {
            while (!string.IsNullOrEmpty(domain))
            {
                if (domainEntries != null)
                {
                    //search domain list
                    foreach (DnsDomainOffset domainEntry in domainEntries)
                    {
                        if (domain.Equals(domainEntry.Domain, StringComparison.OrdinalIgnoreCase))
                        {
                            //found matching domain offset for compression
                            ushort pointer = 0xC000;
                            pointer |= domainEntry.Offset;

                            byte[] pointerBytes = BitConverter.GetBytes(pointer);
                            Array.Reverse(pointerBytes); //convert to network order

                            //write pointer
                            s.Write(pointerBytes, 0, 2);
                            return;
                        }
                    }

                    domainEntries.Add(new DnsDomainOffset(Convert.ToUInt16(s.Position), domain));
                }

                string label;
                int i = domain.IndexOf('.');
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

                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                if (labelBytes.Length > 63)
                    throw new DnsClientException("ConvertDomainToLabel: Invalid domain name. Label cannot exceed 63 bytes.");

                s.WriteByte(Convert.ToByte(labelBytes.Length));
                s.Write(labelBytes, 0, labelBytes.Length);
            }

            s.WriteByte(Convert.ToByte(0));
        }

        public static string DeserializeDomainName(Stream s, int maxDepth = 10)
        {
            if (maxDepth < 0)
                throw new DnsClientException("Error while reading domain name: max depth for decompression reached");

            StringBuilder domain = new StringBuilder();
            byte labelLength = s.ReadBytes(1)[0];
            byte[] buffer = null;

            while (labelLength > 0)
            {
                if ((labelLength & 0xC0) == 0xC0)
                {
                    short Offset = BitConverter.ToInt16(new byte[] { s.ReadBytes(1)[0], (byte)(labelLength & 0x3F) }, 0);
                    long CurrentPosition = s.Position;
                    s.Position = Offset;
                    domain.Append(DeserializeDomainName(s, maxDepth - 1));
                    domain.Append(".");
                    s.Position = CurrentPosition;
                    break;
                }

                if (buffer == null)
                    buffer = new byte[255]; //late buffer init to avoid unnecessary allocation in most cases

                s.ReadBytes(buffer, 0, labelLength);
                domain.Append(Encoding.ASCII.GetString(buffer, 0, labelLength));
                domain.Append(".");
                labelLength = s.ReadBytes(1)[0];
            }

            if (domain.Length > 0)
                domain.Length--;

            return domain.ToString();
        }

        internal static string EncodeCharacterString(string value)
        {
            if (value.Contains(" ") || value.Contains("\t"))
                value = "\"" + value.Replace("\"", "\\\"") + "\"";

            return value;
        }

        internal static string DecodeCharacterString(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
                value = value.Substring(1, value.Length - 2).Replace("\\\"", "\"");

            return value;
        }

        public void WriteToUdp(Stream s)
        {
            WriteHeaders(s, _question.Count, _answer.Count, _authority.Count, _additional.Count);

            List<DnsDomainOffset> domainEntries = new List<DnsDomainOffset>(1);

            for (int i = 0; i < _question.Count; i++)
                _question[i].WriteTo(s, domainEntries);

            for (int i = 0; i < _answer.Count; i++)
                _answer[i].WriteTo(s, domainEntries);

            for (int i = 0; i < _authority.Count; i++)
                _authority[i].WriteTo(s, domainEntries);

            for (int i = 0; i < _additional.Count; i++)
                _additional[i].WriteTo(s, domainEntries);
        }

        private void WriteHeaders(Stream s, int QDCOUNT, int ANCOUNT, int NSCOUNT, int ARCOUNT)
        {
            WriteUInt16NetworkOrder(_ID, s);
            s.WriteByte(Convert.ToByte((_QR << 7) | ((byte)_OPCODE << 3) | (_AA << 2) | (_TC << 1) | _RD));
            s.WriteByte(Convert.ToByte((_RA << 7) | (_Z << 6) | (_AD << 5) | (_CD << 4) | (byte)_RCODE));
            WriteUInt16NetworkOrder(Convert.ToUInt16(QDCOUNT), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(ANCOUNT), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(NSCOUNT), s);
            WriteUInt16NetworkOrder(Convert.ToUInt16(ARCOUNT), s);
        }
    }
}
