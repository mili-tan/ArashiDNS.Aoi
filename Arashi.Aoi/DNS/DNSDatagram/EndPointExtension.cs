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
using System.IO;
using System.Net;
using TechnitiumLibrary.IO;

namespace TechnitiumLibrary.Net
{
    public static class EndPointExtension
    {
        #region static

        public static EndPoint Parse(BinaryReader bR)
        {
            switch (bR.ReadByte())
            {
                case 1:
                    return new IPEndPoint(new IPAddress(bR.ReadBytes(4)), bR.ReadUInt16());

                case 2:
                    return new IPEndPoint(new IPAddress(bR.ReadBytes(16)), bR.ReadUInt16());

                case 3:
                    return new DomainEndPoint(bR.ReadShortString(), bR.ReadUInt16());

                default:
                    throw new NotSupportedException("AddressFamily not supported.");
            }
        }

        #endregion
    }
}
