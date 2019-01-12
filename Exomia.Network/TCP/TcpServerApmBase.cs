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
using Exomia.Network.Native;
using LZ4;

namespace Exomia.Network.TCP
{
    /// <inheritdoc />
    /// <summary>
    ///     A TCP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient">TServerClient</typeparam>
    public abstract class TcpServerApmBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     _maxPacketSize
        /// </summary>
        protected readonly int _maxPacketSize;

        /// <inheritdoc />
        protected TcpServerApmBase(int maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;
        }

        /// <inheritdoc />
        public override SendError SendTo(Socket arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.SerializeTcp(
                    commandid, data, offset, length, responseid, EncryptionMode.None, out byte[] send, out int size);
                try
                {
                    SendStateObject state;
                    state.Socket = arg0;
                    state.Buffer = send;
                    arg0.BeginSend(
                        send, 0, size, SocketFlags.None, BeginSendCallback, state);

                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                    ByteArrayPool.Return(send);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Error);
                    ByteArrayPool.Return(send);
                    return SendError.Socket;
                }
                catch
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                    ByteArrayPool.Return(send);
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
            try
            {
                _listener.BeginAccept(AcceptCallback, null);
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

        private void BeginSendCallback(IAsyncResult iar)
        {
            SendStateObject state = (SendStateObject)iar.AsyncState;
            try
            {
                if (state.Socket.EndSend(iar) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                }
            }
            catch
            {
                /* IGNORE */
            }
            finally
            {
                ByteArrayPool.Return(state.Buffer);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = _listener.EndAccept(ar);
                ServerClientStateObject state = new ServerClientStateObject
                {
                    //0.2mb
                    Socket         = socket,
                    BufferWrite    = new byte[_maxPacketSize],
                    BufferRead     = new byte[_maxPacketSize],
                    CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ReceiveAsync(state);
            }
            catch (ObjectDisposedException)
            {
                /* SOCKET CLOSED */
                return;
            }
            catch
            {
                /* IGNORE */
            }

            ListenAsync();
        }

        private void ReceiveAsync(ServerClientStateObject state)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    state.Socket.BeginReceive(
                        state.BufferWrite, 0, state.BufferWrite.Length, SocketFlags.None, ReceiveDataCallback, state);
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified); }
            }
        }

        private unsafe void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;
            int length;
            try
            {
                if ((length = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Error);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                return;
            }

            CircularBuffer circularBuffer = state.CircularBuffer;
            int size = circularBuffer.Write(state.BufferWrite, 0, length);
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
                            circularBuffer.Write(state.BufferWrite, size, length - size);
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
                        if (Serialization.Serialization.S2E(
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

                            ReceiveAsync(state);
                            DeserializeData(state.Socket, commandID, deserializeBuffer, 0, bufferLength, responseID);
                            return;
                        }
                        break;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += circularBuffer.Write(state.BufferWrite, size, length - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            ReceiveAsync(state);
        }

        private struct SendStateObject
        {
            public byte[] Buffer;
            public Socket Socket;
        }

        private sealed class ServerClientStateObject
        {
            public byte[] BufferWrite;
            public byte[] BufferRead;
            public CircularBuffer CircularBuffer;
            public Socket Socket;
        }
    }
}