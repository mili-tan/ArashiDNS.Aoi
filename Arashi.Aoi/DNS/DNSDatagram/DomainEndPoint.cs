/*
Technitium Library
Copyright (C) 2019  Shreyas Zare (shreyas@technitium.com)

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
using System.Net;
using System.Net.Sockets;

namespace TechnitiumLibrary.Net
{
    public class DomainEndPoint : EndPoint
    {
        #region constructor

        public DomainEndPoint(string address, int port)
        {
            if (address == null)
                throw new ArgumentNullException();

            if (IPAddress.TryParse(address, out _))
                throw new ArgumentException("Address must be a domain name: " + address);

            Address = address;
            Port = port;
        }

        #endregion

        #region public

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            DomainEndPoint other = obj as DomainEndPoint;
            if (other == null)
                return false;

            if (!Address.Equals(other.Address, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Port != other.Port)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ Port.GetHashCode();
        }

        public override string ToString()
        {
            return Address + ":" + Port;
        }

        #endregion

        #region properties

        public override AddressFamily AddressFamily => AddressFamily.Unspecified;

        public string Address { get; }

        public int Port { get; }

        #endregion
    }
}
