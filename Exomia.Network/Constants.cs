#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

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