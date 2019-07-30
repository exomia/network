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
using Exomia.Network.Encoding;
using Exomia.Network.Native;
using K4os.Compression.LZ4;

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
        ///     Size of the maximum packet.
        /// </summary>
        protected readonly ushort _maxPacketSize;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> Size of the maximum packet. </param>
        private protected TcpServerBase(ushort maxPacketSize)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;
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

        /// <summary>
        ///     Receives.
        /// </summary>
        /// <param name="socket">           The socket. </param>
        /// <param name="circularBuffer">   Buffer for circular data. </param>
        /// <param name="bufferWrite">      The buffer write. </param>
        /// <param name="bufferRead">       The buffer read. </param>
        /// <param name="bytesTransferred"> The bytes transferred. </param>
        /// <exception cref="Exception">
        ///     Thrown when an exception error condition
        ///     occurs.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        private protected unsafe void Receive(Socket         socket,
                                              CircularBuffer circularBuffer,
                                              byte[]         bufferWrite,
                                              byte[]         bufferRead,
                                              int            bytesTransferred)
        {
            int size = circularBuffer.Write(bufferWrite, 0, bytesTransferred);
            while (circularBuffer.PeekHeader(
                       0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                && dataLength <= circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (circularBuffer.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = bufferRead)
                    {
                        circularBuffer.Read(ptr, 0, dataLength, Constants.TCP_HEADER_SIZE);
                        if (size < bytesTransferred)
                        {
                            circularBuffer.Write(bufferWrite, size, bytesTransferred - size);
                        }

                        uint responseID = 0;
                        int  offset     = 0;
                        if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                        {
                            responseID = *(uint*)ptr;
                            offset     = 4;
                        }

                        CompressionMode compressionMode =
                            (CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK);
                        if (compressionMode != CompressionMode.None)
                        {
                            offset += 4;
                        }

                        byte[] deserializeBuffer = ByteArrayPool.Rent(dataLength);
                        if (PayloadEncoding.Decode(
                                ptr, offset, dataLength - 1, deserializeBuffer, out int bufferLength) == checksum)
                        {
                            switch (compressionMode)
                            {
                                case CompressionMode.None:
                                    break;
                                case CompressionMode.Lz4:
                                    int l = *(int*)(ptr + offset);

                                    byte[] buffer = ByteArrayPool.Rent(l);
                                    int    s      = LZ4Codec.Decode(deserializeBuffer, 0, bufferLength, buffer, 0, l);
                                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                    ByteArrayPool.Return(deserializeBuffer);
                                    deserializeBuffer = buffer;
                                    bufferLength      = l;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(CompressionMode),
                                        (CompressionMode)(packetHeader &
                                                          Serialization.Serialization.COMPRESSED_MODE_MASK),
                                        "Not supported!");
                            }
                            DeserializeData(socket, commandID, deserializeBuffer, 0, bufferLength, responseID);
                        }
                        return;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < bytesTransferred)
                {
                    size += circularBuffer.Write(bufferWrite, size, bytesTransferred - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { return; }
            }
        }
    }
}