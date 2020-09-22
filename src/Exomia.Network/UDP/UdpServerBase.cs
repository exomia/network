#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     An UDP server base.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract class UdpServerBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        private readonly           ushort                          _maxPayloadSize;
        private protected readonly BigDataHandler<(EndPoint, int)> _bigDataHandler;

        /// <inheritdoc />
        private protected override ushort MaxPayloadSize
        {
            get { return _maxPayloadSize; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpServerEapBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        private protected UdpServerBase(ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(16)
        {
            _maxPayloadSize =
                expectedMaxPayloadSize > 0 && expectedMaxPayloadSize < Constants.UDP_PAYLOAD_SIZE_MAX
                    ? expectedMaxPayloadSize
                    : Constants.TCP_PAYLOAD_SIZE_MAX;

            _bigDataHandler = new BigDataHandler<(EndPoint, int)>.Timed();
        }

        /// <inheritdoc />
        private protected override void Configure()
        {
            ReceiveBufferSize = 0; //0kb
            SendBufferSize    = 0; //0kb
        }

        /// <inheritdoc />
#if NETSTANDARD2_1
        private protected override bool OnRun(int port, [NotNullWhen(true)] out Socket? listener)
#else
        private protected override bool OnRun(int port, out Socket? listener)
#endif
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false, DualMode = true
                    };
                    listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false
                    };
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
        }
    }
}