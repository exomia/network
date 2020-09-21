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
    ///     Information about the deserialize packet.
    /// </summary>
    struct DeserializePacketInfo
    {
        /// <summary>
        ///     Identifier for the command or response.
        /// </summary>
        public uint CommandOrResponseID;

        /// <summary>
        ///     True if this object is response bit set.
        /// </summary>
        public bool IsResponseBitSet;

        /// <summary>
        ///     Identifier for the request.
        /// </summary>
        public uint RequestID;

        /// <summary>
        ///     The data.
        /// </summary>
        public byte[] Data;

        /// <summary>
        ///     The length.
        /// </summary>
        public int Length;
    }
}