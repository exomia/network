#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     An UDP client base.
    /// </summary>
    public abstract class UdpClientBase : ClientBase
    {
        private readonly           ushort              _maxPayloadSize;
        private protected readonly BigDataHandler<int> _bigDataHandler;

        /// <inheritdoc />
        private protected override ushort MaxPayloadSize
        {
            get { return _maxPayloadSize; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpServerEapBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        private protected UdpClientBase(ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(4)
        {
            _maxPayloadSize =
                expectedMaxPayloadSize > 0 && expectedMaxPayloadSize < Constants.UDP_PAYLOAD_SIZE_MAX
                    ? expectedMaxPayloadSize
                    : Constants.UDP_PAYLOAD_SIZE_MAX;

            _bigDataHandler = new BigDataHandler<int>.Timed();
        }

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _bigDataHandler.Dispose();
            }
        }

        /// <inheritdoc />
        private protected override void Configure()
        {
            ReceiveBufferSize = 0; //0kb
            SendBufferSize    = 0; //0kb
        }

        /// <inheritdoc />
        private protected override bool TryCreateSocket([NotNullWhen(true)] out Socket? socket)
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