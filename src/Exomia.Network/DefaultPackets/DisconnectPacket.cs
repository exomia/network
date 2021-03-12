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
    ///     A disconnect packet.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct DisconnectPacket
    {
        /// <summary>
        ///     The reason.
        /// </summary>
        public DisconnectReason Reason;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DisconnectPacket" /> struct.
        /// </summary>
        /// <param name="reason"> The reason. </param>
        public DisconnectPacket(DisconnectReason reason)
        {
            Reason = reason;
        }
    }
}