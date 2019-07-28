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
    ///     A UDP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class UdpClientEap : ClientBase
    {
        /// <summary>
        ///     Size of the maximum packet.
        /// </summary>
        private readonly int _maxPacketSize;

        /// <summary>
        ///     Socket asynchronous event information.
        /// </summary>
        private readonly SocketAsyncEventArgs _receiveEventArgs;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        public UdpClientEap(int maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.UDP_PACKET_SIZE_MAX;

            _receiveEventArgs           =  new SocketAsyncEventArgs();
            _receiveEventArgs.Completed += ReceiveAsyncCompleted;
            _receiveEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);

            _sendEventArgsPool = new SocketAsyncEventArgsPool();
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

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!_clientSocket.ReceiveAsync(_receiveEventArgs))
                    {
                        ReceiveAsyncCompleted(_receiveEventArgs.AcceptSocket, _receiveEventArgs);
                    }
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        private protected override unsafe SendError BeginSendData(uint   commandID,
                                                                  byte[] data,
                                                                  int    offset,
                                                                  int    length,
                                                                  uint   responseID)
        {
            if (_clientSocket == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();
                if (sendEventArgs == null)
                {
                    sendEventArgs           =  new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }

                fixed (byte* src = data)
                fixed (byte* dst = sendEventArgs.Buffer)
                {
                    Serialization.Serialization.SerializeUdp(
                        commandID, src + offset, length, responseID, EncryptionMode.None,
                        CompressionMode.Lz4, dst, out int size);
                    sendEventArgs.SetBuffer(0, size);
                }

                try
                {
                    if (!_clientSocket.SendAsync(sendEventArgs))
                    {
                        SendAsyncCompleted(_clientSocket, sendEventArgs);
                    }
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    Disconnect(DisconnectReason.Aborted);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    Disconnect(DisconnectReason.Error);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Socket;
                }
                catch
                {
                    Disconnect(DisconnectReason.Unspecified);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

        /// <summary>
        ///     Receive asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private unsafe void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
                return;
            }
            if (e.BytesTransferred <= 0)
            {
                Disconnect(DisconnectReason.Graceful);
                return;
            }

            e.Buffer.GetHeaderUdp(out byte packetHeader, out uint commandID, out int dataLength);

            if (e.BytesTransferred == dataLength + Constants.UDP_HEADER_SIZE)
            {
                uint responseID = 0;
                int  offset     = 0;
                fixed (byte* src = e.Buffer)
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
                                e.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l);
                            if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                            ReceiveAsync();
                            DeserializeData(commandID, payload, 0, l, responseID);
                            break;
                        case CompressionMode.None:
                            dataLength -= offset;
                            payload    =  ByteArrayPool.Rent(dataLength);

                            fixed (byte* dest = payload)
                            {
                                Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }

                            ReceiveAsync();
                            DeserializeData(commandID, payload, 0, dataLength, responseID);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(CompressionMode),
                                (CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK),
                                "Not supported!");
                    }
                }
                return;
            }
            ReceiveAsync();
        }

        /// <summary>
        ///     Sends an asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void SendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }
    }
}