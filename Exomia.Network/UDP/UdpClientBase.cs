#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     An UDP client base.
    /// </summary>
    public abstract class UdpClientBase : ClientBase
    {
        /// <summary>
        ///     Size of the maximum packet.
        /// </summary>
        protected readonly ushort _maxPacketSize;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpClientBase" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> Size of the maximum packet. </param>
        private protected UdpClientBase(ushort maxPacketSize)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.UDP_PACKET_SIZE_MAX;
        }

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

        private protected unsafe void Receive(byte[] buffer, int bytesTransferred)
        {
            ReceiveAsync();

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
                            fixed (byte* dst = payload)
                            {
                                int s = LZ4Codec.Decode(
                                    src + Constants.UDP_HEADER_SIZE + offset, dataLength - offset, dst, l);
                                if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                            }
                            DeserializeData(commandID, payload, 0, l, responseID);
                            break;
                        case CompressionMode.None:
                            dataLength -= offset;
                            payload    =  ByteArrayPool.Rent(dataLength);

                            fixed (byte* dest = payload)
                            {
                                Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }
                            DeserializeData(commandID, payload, 0, dataLength, responseID);
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