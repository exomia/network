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
    ///     Information about the deserialize packet.
    /// </summary>
    struct DeserializePacketInfo
    {
        /// <summary>
        ///     Identifier for the command.
        /// </summary>
        public uint CommandID;

        /// <summary>
        ///     Identifier for the response.
        /// </summary>
        public uint ResponseID;

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