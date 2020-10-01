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

namespace TechnitiumLibrary.Net.Dns
{
    public abstract class DnsResourceRecordData
    {
        protected ushort _length;

        protected abstract void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries);

        public void WriteTo(Stream s, List<DnsDomainOffset> domainEntries)
        {
            long originalPosition = s.Position;

            //write dummy RDLENGTH
            s.Write(new byte[] { 0, 0 }, 0, 2);

            //write RDATA
            WriteRecordData(s, domainEntries);

            long finalPosition = s.Position;

            //write actual RDLENGTH
            ushort length = Convert.ToUInt16(finalPosition - originalPosition - 2);
            s.Position = originalPosition;
            DnsDatagram.WriteUInt16NetworkOrder(length, s);

            s.Position = finalPosition;
        }

        public override abstract bool Equals(object obj);

        public override abstract int GetHashCode();

        public override abstract string ToString();
    }
}
