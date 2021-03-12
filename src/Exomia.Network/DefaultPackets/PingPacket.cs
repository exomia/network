#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.InteropServices;

namespace Exomia.Network.DefaultPackets
{
    /// <summary>
    ///     A ping packet.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public struct PingPacket
    {
        /// <summary>
        ///     The timestamp.
        /// </summary>
        public long Timestamp;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PingPacket" /> struct.
        /// </summary>
        /// <param name="timestamp"> The timestamp. </param>
        public PingPacket(long timestamp)
        {
            Timestamp = timestamp;
        }
    }
}