#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Exomia.Network.UnitTest")]

namespace Exomia.Network
{
    /// <summary>
    ///     A constants.
    /// </summary>
    static class Constants
    {
        /// <summary>
        ///     Size of the TCP header.
        /// </summary>
        internal const int TCP_HEADER_SIZE = 7;

        /// <summary>
        ///     The TCP packet size maximum.
        /// </summary>
        internal const int TCP_PACKET_SIZE_MAX = 65535 - TCP_HEADER_SIZE - 8;

        /// <summary>
        ///     The zero byte.
        /// </summary>
        internal const byte ZERO_BYTE = 0;

        /// <summary>
        ///     Size of the UDP header.
        /// </summary>
        internal const int UDP_HEADER_SIZE = 5;

        /// <summary>
        ///     The UDP packet size maximum.
        /// </summary>
        internal const int UDP_PACKET_SIZE_MAX = 65535 - UDP_HEADER_SIZE - 8;

        /// <summary>
        ///     The user command limit.
        /// </summary>
        internal const uint USER_COMMAND_LIMIT = 65500;
    }
}