#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

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
        ///     Size of the maximum packet.
        /// </summary>
        protected readonly ushort _maxPacketSize;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpServerEapBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> Size of the maximum packet. </param>
        protected UdpServerBase(ushort maxPacketSize)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.UDP_PACKET_SIZE_MAX;
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

        /// <summary>
        ///     Receives.
        /// </summary>
        /// <param name="buffer">           The buffer. </param>
        /// <param name="bytesTransferred"> The bytes transferred. </param>
        /// <param name="ep">               The ep. </param>
        /// <exception cref="Exception">
        ///     Thrown when an exception error condition
        ///     occurs.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        private protected unsafe void Receive(byte[] buffer, int bytesTransferred, EndPoint ep)
        {
            buffer.GetHeaderUdp(out byte packetHeader, out uint commandID, out int dataLength);

            if (bytesTransferred == dataLength + Constants.UDP_HEADER_SIZE)
            {
                uint responseID = 0;
                int  offset     = 0;
                fixed (byte* src = buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset     = 4;
                    }
                    byte[] payload;
                    switch ((CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK))
                    {
                        case CompressionMode.Lz4:
                            int l = *(int*)(src + offset);
                            offset += 4;

                            payload = ByteArrayPool.Rent(l);
                            int s = LZ4Codec.Decode(
                                buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l);
                            if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                            DeserializeData(ep, commandID, payload, 0, l, responseID);
                            break;
                        case CompressionMode.None:
                            dataLength -= offset;
                            payload    =  ByteArrayPool.Rent(dataLength);

                            fixed (byte* dest = payload)
                            {
                                Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }

                            DeserializeData(ep, commandID, payload, 0, dataLength, responseID);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(CompressionMode),
                                (CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK),
                                "Not supported!");
                    }
                }
            }
        }
    }
}