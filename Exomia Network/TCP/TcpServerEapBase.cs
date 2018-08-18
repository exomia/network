#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
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
using Exomia.Network.Serialization;
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
        protected TcpServerEapBase(int maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;

            _sendEventArgsPool = new SocketAsyncEventArgsPool(128);
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
                    sendEventArgs = new SocketAsyncEventArgs();
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

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
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
        protected override void ListenAsync()
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
            if (e.BytesTransferred <= 0)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Graceful);
                return;
            }

            //TODO: circular buffer
            e.Buffer.GetHeaderUdp(out uint commandID, out int dataLength, out byte h1);

            if (dataLength == e.BytesTransferred - Constants.TCP_HEADER_SIZE)
            {
                Socket socket = e.AcceptSocket;

                uint responseID = 0;
                byte[] data;
                if ((h1 & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                {
                    int l;
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = e.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.TCP_HEADER_SIZE);
                            l = *(int*)(ptr + Constants.TCP_HEADER_SIZE + 4);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            e.Buffer, Constants.TCP_HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        fixed (byte* ptr = e.Buffer)
                        {
                            l = *(int*)(ptr + Constants.TCP_HEADER_SIZE);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            e.Buffer, Constants.TCP_HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    ReceiveAsync(e);
                    DeserializeData(socket, commandID, data, 0, l, responseID);
                }
                else
                {
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = e.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.TCP_HEADER_SIZE);
                        }
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(e.Buffer, Constants.TCP_HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(e.Buffer, Constants.TCP_HEADER_SIZE, data, 0, dataLength);
                    }

                    ReceiveAsync(e);
                    DeserializeData(socket, commandID, data, 0, dataLength, responseID);
                }
                return;
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
    }
}