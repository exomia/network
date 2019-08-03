#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     A constants.
    /// </summary>
    static class Constants
    {
        /// <summary>
        ///     The safety payload offset.
        /// </summary>
        internal const int SAFETY_PAYLOAD_OFFSET = 20;

        /// <summary>
        ///     Size of the TCP header.
        /// </summary>
        internal const int TCP_HEADER_SIZE = 7;

        /// <summary>
        ///     The TCP header offset.
        /// </summary>
        internal const int TCP_HEADER_OFFSET = TCP_HEADER_SIZE + SAFETY_PAYLOAD_OFFSET;

        /// <summary>
        ///     The TCP maximum payload size.
        /// </summary>
        internal const ushort TCP_PAYLOAD_SIZE_MAX = ushort.MaxValue - TCP_HEADER_OFFSET - 1 - 8189;

        /// <summary>
        ///     Size of the UDP header.
        /// </summary>
        internal const int UDP_HEADER_SIZE = 5;

        /// <summary>
        ///     The UDP header offset.
        /// </summary>
        internal const int UDP_HEADER_OFFSET = UDP_HEADER_SIZE + SAFETY_PAYLOAD_OFFSET;

        /// <summary>
        ///     The UDP maximum payload size.
        /// </summary>
        internal const ushort UDP_PAYLOAD_SIZE_MAX = ushort.MaxValue - UDP_HEADER_OFFSET;

        /// <summary>
        ///     The user command limit.
        /// </summary>
        internal const uint USER_COMMAND_LIMIT = 65500;

        /// <summary>
        ///     The command identifier shift.
        /// </summary>
        internal const int COMMAND_ID_SHIFT = 16;

        /// <summary>
        ///     The data length mask.
        /// </summary>
        internal const int DATA_LENGTH_MASK = 0xFFFF;

        /// <summary>
        ///     LENGTH_THRESHOLD 4096.
        /// </summary>
        internal const int LENGTH_THRESHOLD = 1 << 12;

        /// <summary>
        ///     The response bit.
        /// </summary>
        internal const byte RESPONSE_1_BIT = 1 << 6;

        /// <summary>
        ///     The is chunked bit.
        /// </summary>
        internal const byte IS_CHUNKED_1_BIT = 1 << 7;

        /// <summary>
        ///     The compressed mode mask.
        /// </summary>
        internal const byte COMPRESSED_MODE_MASK = 0b0011_1000;

        /// <summary>
        ///     The response bit mask.
        /// </summary>
        internal const uint RESPONSE_BIT_MASK = 0b0100_0000;

        /// <summary>
        ///     The is chunked bit mask.
        /// </summary>
        internal const uint IS_CHUNKED_BIT_MASK = 0b1000_0000;

        /// <summary>
        ///     The zero byte.
        /// </summary>
        internal const byte ZERO_BYTE = 0;
    }
}