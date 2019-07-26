﻿#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using Exomia.Network.Buffers;
using Exomia.Network.Encoding;
using Exomia.Network.Native;
using K4os.Compression.LZ4;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class TcpClientEap : ClientBase
    {
        /// <summary>
        ///     Size of the maximum packet.
        /// </summary>
        private readonly int _maxPacketSize;

        /// <summary>
        ///     Buffer for circular data.
        /// </summary>
        private readonly CircularBuffer _circularBuffer;

        /// <summary>
        ///     The buffer read.
        /// </summary>
        private readonly byte[] _bufferRead;

        /// <summary>
        ///     Socket asynchronous event information.
        /// </summary>
        private readonly SocketAsyncEventArgs _receiveEventArgs;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        public TcpClientEap(ushort maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;
            _bufferRead     = new byte[_maxPacketSize];
            _circularBuffer = new CircularBuffer(_maxPacketSize * 2);

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

        private protected override unsafe SendError BeginSendData(uint   commandid,
                                                                  byte[] data,
                                                                  int    offset,
                                                                  int    length,
                                                                  uint   responseid)
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
                    Serialization.Serialization.SerializeTcp(
                        commandid, src + offset, length, responseid, EncryptionMode.None,
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
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    Disconnect(DisconnectReason.Error);
                    return SendError.Socket;
                }
                catch
                {
                    Disconnect(DisconnectReason.Unspecified);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            _circularBuffer.Dispose();
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

            int length = e.BytesTransferred;
            if (length <= 0)
            {
                Disconnect(DisconnectReason.Graceful);
                return;
            }

            int size = _circularBuffer.Write(e.Buffer, 0, length);
            while (_circularBuffer.PeekHeader(
                       0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                && dataLength <= _circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (_circularBuffer.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = _bufferRead)
                    {
                        _circularBuffer.Read(ptr, 0, dataLength, Constants.TCP_HEADER_SIZE);
                        if (size < length)
                        {
                            _circularBuffer.Write(e.Buffer, size, length - size);
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

                            ReceiveAsync();
                            DeserializeData(commandID, deserializeBuffer, 0, bufferLength, responseID);
                            return;
                        }
                        break;
                    }
                }
                bool skipped = _circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += _circularBuffer.Write(e.Buffer, size, length - size);
                }
                if (!skipped && !_circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
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