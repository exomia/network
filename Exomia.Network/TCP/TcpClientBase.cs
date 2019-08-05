#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net.Sockets;
using Exomia.Network.Encoding;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP client base.
    /// </summary>
    public abstract class TcpClientBase : ClientBase
    {
        /// <summary>
        ///     Size of the payload.
        /// </summary>
        private protected readonly ushort _payloadSize;

        /// <summary>
        ///     Size of the maximum payload.
        /// </summary>
        private readonly ushort _maxPayloadSize;

        /// <inheritdoc />
        private protected override ushort MaxPayloadSize
        {
            get { return _maxPayloadSize; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        private protected TcpClientBase(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
        {
            _maxPayloadSize = expectedMaxPayloadSize > 0 && expectedMaxPayloadSize < Constants.TCP_PAYLOAD_SIZE_MAX
                ? expectedMaxPayloadSize
                : Constants.TCP_PAYLOAD_SIZE_MAX;
            _payloadSize = (ushort)(PayloadEncoding.EncodedPayloadLength(_maxPayloadSize) + 1);
        }

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