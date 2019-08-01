#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net.Sockets;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     An UDP client base.
    /// </summary>
    public abstract class UdpClientBase : ClientBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpClientBase" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> Size of the maximum packet. </param>
        private protected UdpClientBase(ushort maxPacketSize)
            : base(
                maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                    ? maxPacketSize
                    : Constants.UDP_PACKET_SIZE_MAX) { }

        /// <inheritdoc />
        private protected override bool TryCreateSocket(out Socket socket)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false, DualMode = true
                    };
                }
                else
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false
                    };
                }
                return true;
            }
            catch
            {
                socket = null;
                return false;
            }
        }
    }
}