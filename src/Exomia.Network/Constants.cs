#region License

// Copyright (c) 2018-2020, exomia
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
        internal const int    SAFETY_PAYLOAD_OFFSET        = 20;
        internal const int    TCP_HEADER_SIZE              = 7;
        internal const int    TCP_HEADER_OFFSET            = TCP_HEADER_SIZE + SAFETY_PAYLOAD_OFFSET;
        internal const ushort TCP_PAYLOAD_SIZE_MAX         = 65535 - TCP_HEADER_OFFSET - 1 - 8189;
        internal const int    UDP_HEADER_SIZE              = 5;
        internal const int    UDP_HEADER_OFFSET            = UDP_HEADER_SIZE + SAFETY_PAYLOAD_OFFSET;
        internal const ushort UDP_PAYLOAD_SIZE_MAX         = 65507 - UDP_HEADER_OFFSET;
        internal const uint   USER_COMMAND_LIMIT           = 65500;
        internal const int    COMMAND_OR_RESPONSE_ID_SHIFT = 16;
        internal const int    DATA_LENGTH_MASK             = 0xFFFF;
        internal const int    LENGTH_THRESHOLD             = 1 << 12;
        internal const byte   RESPONSE_1_BIT               = 1 << 5;
        internal const byte   REQUEST_1_BIT                = 1 << 6;
        internal const byte   IS_CHUNKED_1_BIT             = 1 << 7;
        internal const byte   COMPRESSED_MODE_MASK         = 0b0001_1000;
        internal const uint   RESPONSE_BIT_MASK            = 0b0010_0000;
        internal const uint   REQUEST_BIT_MASK             = 0b0100_0000;
        internal const uint   IS_CHUNKED_BIT_MASK          = 0b1000_0000;
        internal const byte   ZERO_BYTE                    = 0;
    }
}