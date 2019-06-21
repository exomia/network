#region License

// Copyright (c) 2018-2019, exomia
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
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct ConnectPacket
    {

        /// <summary>
        ///     Gets the checksum[16].
        /// </summary>
        /// <value>
        ///     The checksum[16].
        /// </value>
        public fixed byte Checksum[16];
    }
}