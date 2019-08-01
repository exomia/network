#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net.Sockets;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP client base.
    /// </summary>
    public abstract class TcpClientBase : ClientBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpClientBase" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> Size of the maximum packet. </param>
        private protected TcpClientBase(ushort maxPacketSize)
            : base(
                maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                    ? maxPacketSize
                    : Constants.TCP_PACKET_SIZE_MAX) { }

        /// <summary>
        ///     Attempts to create socket.
        /// </summary>
        /// <param name="socket"> [out] The socket. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private protected override bool TryCreateSocket(out Socket socket)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false, DualMode = true
                    };
                }
                else
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false
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