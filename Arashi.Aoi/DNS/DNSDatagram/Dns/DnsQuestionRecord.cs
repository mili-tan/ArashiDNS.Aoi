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

using System.Collections.Generic;
using System.IO;

namespace TechnitiumLibrary.Net.Dns
{
    //DNS Query Name Minimisation to Improve Privacy
    //https://tools.ietf.org/html/draft-ietf-dnsop-rfc7816bis-04

    public class DnsQuestionRecord
    {
        readonly string _name;
        readonly DnsResourceRecordType _type;
        readonly DnsClass _class;

        public DnsQuestionRecord(dynamic jsonQuestionRecord)
        {
            _name = (jsonQuestionRecord.name.Value as string).TrimEnd('.');
            _type = (DnsResourceRecordType)jsonQuestionRecord.type;
            _class = DnsClass.IN;
        }

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.SerializeDomainName(_name, s, domainEntries);
            DnsDatagram.WriteUInt16NetworkOrder((ushort) _type, s);
            DnsDatagram.WriteUInt16NetworkOrder((ushort) _class, s);
        }
    }
}
