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
    public abstract class TcpServerBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        #region Variables

        /// <summary>
        ///     _maxPacketSize
        /// </summary>
        protected readonly int _maxPacketSize;

        #endregion

        #region Constructors

        /// <inheritdoc />
        protected TcpServerBase(int maxPacketSize = 0)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.PACKET_SIZE_MAX;
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    Blocking = false
                };
                listener.Bind(new IPEndPoint(IPAddress.Any, port));
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
            try
            {
                _listener.BeginAccept(AcceptCallback, null);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        protected override void BeginSendDataTo(Socket arg0, byte[] send, int offset, int lenght)
        {
            if (arg0 == null)
            {
                ByteArrayPool.Return(send);
                return;
            }

            try
            {
                arg0.BeginSend(
                    send, offset, lenght, SocketFlags.None, iar =>
                    {
                        try
                        {
                            if (arg0.EndSend(iar) <= 0)
                            {
                                InvokeClientDisconnected(arg0);
                            }
                        }
                        finally
                        {
                            ByteArrayPool.Return(send);
                        }
                    }, null);
            }
            catch
            {
                ByteArrayPool.Return(send);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = _listener.EndAccept(ar);
                ServerClientStateObject state = new ServerClientStateObject
                {
                    Socket = socket,
                    Buffer = new byte[_maxPacketSize]
                };

                ReceiveAsync(state);
            }
            catch
            {
                /* IGNORE */
            }

            ListenAsync();
        }

        private void ReceiveAsync(ServerClientStateObject state)
        {
            try
            {
                state.Socket.BeginReceive(
                    state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveDataCallback, state);
            }
            catch { InvokeClientDisconnected(state.Socket); }
        }

        private unsafe void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;
            int length;
            try
            {
                if ((length = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnected(state.Socket);
                    return;
                }
            }
            catch
            {
                InvokeClientDisconnected(state.Socket);
                return;
            }

            state.Buffer.GetHeader(out uint commandID, out int dataLength, out byte h1);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if ((h1 & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                {
                    int l;
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                            l = *(int*)(ptr + Constants.HEADER_SIZE + 4);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            l = *(int*)(ptr + Constants.HEADER_SIZE);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    DeserializeData(state.Socket, commandID, data, 0, l, responseID);
                }
                else
                {
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                        }
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                    }

                    DeserializeData(state.Socket, commandID, data, 0, dataLength, responseID);
                }
            }

            ReceiveAsync(state);
        }

        #endregion

        #region Nested

        private sealed class ServerClientStateObject
        {
            #region Variables

            public byte[] Buffer;
            public Socket Socket;

            #endregion
        }

        #endregion
    }
}