#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net;
using System.Net.Sockets;
using Exomia.Network.Encoding;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP server base.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract class TcpServerBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
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
        protected TcpServerBase(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
        {
            _maxPayloadSize = expectedMaxPayloadSize > 0 && expectedMaxPayloadSize < Constants.TCP_PAYLOAD_SIZE_MAX
                ? expectedMaxPayloadSize
                : Constants.TCP_PAYLOAD_SIZE_MAX;
            _payloadSize = (ushort)(PayloadEncoding.EncodedPayloadLength(_maxPayloadSize) + 1);
        }

        /// <inheritdoc />
        private protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false, DualMode = true
                    };
                    listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false
                    };
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                listener.Listen(100);
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
        }

        /// <inheritdoc />
        private protected override void OnAfterClientDisconnect(TServerClient client)
        {
            try
            {
                client.Arg0?.Shutdown(SocketShutdown.Both);
                client.Arg0?.Close(CLOSE_TIMEOUT);
            }
            catch
            {
                /* IGNORE */
            }
        }
    }
}