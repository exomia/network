#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

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
        }

        /// <summary>
        ///     Executes the run action.
        /// </summary>
        /// <param name="port">     The port. </param>
        /// <param name="listener"> [out] The listener. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private protected override bool OnRun(int port, out Socket listener)
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