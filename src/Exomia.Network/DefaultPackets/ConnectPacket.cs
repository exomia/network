#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.InteropServices;

namespace Exomia.Network.DefaultPackets
{
    /// <summary>
    ///     A connect packet.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 25)]
    public unsafe struct ConnectPacket
    {
        /// <summary>
        ///     Gets or sets the checksum[16].
        /// </summary>
        public fixed byte Checksum[16];

        /// <summary>
        ///     The nonce.
        /// </summary>
        public ulong Nonce;

        /// <summary>
        ///     True if rejected.
        /// </summary>
        public bool Rejected;
    }
}