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
using System.Runtime.Serialization;

namespace TechnitiumLibrary.Net.Dns
{
    //DNS Query Name Minimisation to Improve Privacy
    //https://tools.ietf.org/html/draft-ietf-dnsop-rfc7816bis-04

    public class DnsQuestionRecord
    {
        #region variables

        readonly string _name;
        readonly DnsResourceRecordType _type;
        readonly DnsClass _class;

        //QNAME Minimization
        const int MAX_MINIMISE_COUNT = 10;
        string _zoneCut;
        string _minimizedName;

        #endregion

        public DnsQuestionRecord(dynamic jsonQuestionRecord)
        {
            _name = (jsonQuestionRecord.name.Value as string).TrimEnd('.');
            _type = (DnsResourceRecordType)jsonQuestionRecord.type;
            _class = DnsClass.IN;
        }

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            if (_minimizedName == null)
            {
                DnsDatagram.SerializeDomainName(_name, s, domainEntries);
                DnsDatagram.WriteUInt16NetworkOrder((ushort)_type, s);
                DnsDatagram.WriteUInt16NetworkOrder((ushort)_class, s);
            }
            else
            {
                DnsDatagram.SerializeDomainName(_minimizedName, s, domainEntries);
                DnsDatagram.WriteUInt16NetworkOrder((ushort)MinimizedType, s);
                DnsDatagram.WriteUInt16NetworkOrder((ushort)_class, s);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            DnsQuestionRecord other = obj as DnsQuestionRecord;
            if (other == null)
                return false;

            if (!_name.Equals(other._name, StringComparison.OrdinalIgnoreCase))
                return false;

            if (_type != other._type)
                return false;

            if (_class != other._class)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }

        #region properties

        public string Name
        { get { return _name; } }

        [IgnoreDataMember]
        public DnsResourceRecordType MinimizedType
        {
            get
            {
                if (_type == DnsResourceRecordType.AAAA)
                    return DnsResourceRecordType.AAAA;

                return DnsResourceRecordType.A;
            }
        }

        #endregion
    }
}
