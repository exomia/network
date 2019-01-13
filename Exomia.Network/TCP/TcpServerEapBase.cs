#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Buffers;
using Exomia.Network.Encoding;
using Exomia.Network.Native;
using LZ4;

namespace Exomia.Network.TCP
{
    /// <inheritdoc />
    /// <summary>
    ///     A TCP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient">TServerClient</typeparam>
    public abstract class TcpServerEapBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     _maxPacketSize
        /// </summary>
        protected readonly int _maxPacketSize;

        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        protected TcpServerEapBase(uint expectedMaxClient = 32, int maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;

            _sendEventArgsPool = new SocketAsyncEventArgsPool(expectedMaxClient);
        }

        /// <inheritdoc />
        public override SendError SendTo(Socket arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();

                if (sendEventArgs == null)
                {
                    sendEventArgs           =  new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }

                Serialization.Serialization.SerializeTcp(
                    commandid, data, offset, length, responseid, EncryptionMode.None, sendEventArgs.Buffer,
                    out int size);
                sendEventArgs.SetBuffer(0, size);
                sendEventArgs.AcceptSocket = arg0;

                try
                {
                    if (!arg0.SendAsync(sendEventArgs))
                    {
                        SendAsyncCompleted(arg0, sendEventArgs);
                    }
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Error);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Socket;
                }
                catch
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

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

        private protected override void ListenAsync()
        {
            SocketAsyncEventArgs acceptArgs = new SocketAsyncEventArgs();
            acceptArgs.Completed += AcceptAsyncCompleted;
            ListenAsync(acceptArgs);
        }

        /// <inheritdoc />
        protected override void OnAfterClientDisconnect(Socket arg0)
        {
            try
            {
                arg0?.Shutdown(SocketShutdown.Both);
                arg0?.Close(CLOSE_TIMEOUT);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void ListenAsync(SocketAsyncEventArgs acceptArgs)
        {
            acceptArgs.AcceptSocket = null;
            try
            {
                if (!_listener.AcceptAsync(acceptArgs))
                {
                    AcceptAsyncCompleted(_listener, acceptArgs);
                }
            }
            catch (ObjectDisposedException)
            {
                /* SOCKET CLOSED */
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                try
                {
                    e.AcceptSocket?.Shutdown(SocketShutdown.Both);
                    e.AcceptSocket?.Close(CLOSE_TIMEOUT);
                }
                catch
                {
                    /* IGNORE */
                }
                ListenAsync(e);
                return;
            }
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs { AcceptSocket = e.AcceptSocket };
                receiveArgs.Completed += ReceiveAsyncCompleted;
                receiveArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                receiveArgs.UserToken = new ServerClientStateObject
                {
                    BufferRead = new byte[_maxPacketSize], CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ListenAsync(e);

                ReceiveAsync(receiveArgs);
            }
        }

        private void ReceiveAsync(SocketAsyncEventArgs args)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!args.AcceptSocket.ReceiveAsync(args))
                    {
                        ReceiveAsyncCompleted(args.AcceptSocket, args);
                    }
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Unspecified); }
            }
        }

        private unsafe void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
                return;
            }
            int length = e.BytesTransferred;
            if (length <= 0)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Graceful);
                return;
            }

            ServerClientStateObject state = (ServerClientStateObject)e.UserToken;
            CircularBuffer circularBuffer = state.CircularBuffer;

            int size = circularBuffer.Write(e.Buffer, 0, length);
            while (circularBuffer.PeekHeader(
                       0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                   && dataLength <= circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (circularBuffer.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = state.BufferRead)
                    {
                        circularBuffer.Read(ptr, 0, dataLength, Constants.TCP_HEADER_SIZE);
                        if (size < length)
                        {
                            circularBuffer.Write(e.Buffer, size, length - size);
                        }

                        uint responseID = 0;
                        int offset = 0;
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
                                case CompressionMode.Lz4:
                                    int l = *(int*)(ptr + offset);

                                    byte[] buffer = ByteArrayPool.Rent(l);
                                    int s = LZ4Codec.Decode(
                                        deserializeBuffer, 0, bufferLength, buffer, 0, l, true);
                                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                    ByteArrayPool.Return(deserializeBuffer);
                                    deserializeBuffer = buffer;
                                    bufferLength      = l;
                                    break;
                            }

                            ReceiveAsync(e);
                            DeserializeData(e.AcceptSocket, commandID, deserializeBuffer, 0, bufferLength, responseID);
                            return;
                        }
                        break;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += circularBuffer.Write(e.Buffer, size, length - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            ReceiveAsync(e);
        }

        private void SendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        private sealed class ServerClientStateObject
        {
            public byte[] BufferRead;
            public CircularBuffer CircularBuffer;
        }
    }
}