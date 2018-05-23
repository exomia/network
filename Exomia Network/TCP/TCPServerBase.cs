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

namespace Exomia.Network.TCP
{
    /// <inheritdoc />
    public abstract class TcpServerBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        #region Constructors

        /// <inheritdoc />
        protected TcpServerBase() { }

        /// <inheritdoc />
        protected TcpServerBase(int maxDataSize)
            : base(maxDataSize) { }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Any, port));
                listener.Listen(100);

                Listen();
                return true;
            }
            catch
            {
                /*IGNORE */
            }

            listener = null;
            return false;
        }

        /// <inheritdoc />
        protected override void BeginSendDataTo(Socket arg0, byte[] send, int lenght)
        {
            if (arg0 == null) { return; }

            try
            {
                arg0.BeginSend(
                    send, 0, lenght, SocketFlags.None, iar =>
                    {
                        arg0.EndSend(iar);
                        ByteArrayPool.Return(send);
                    }, null);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void Listen()
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

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = _listener.EndAccept(ar);

                InvokeClientConnected(socket);

                ServerClientStateObject state = new ServerClientStateObject
                {
                    Socket = socket,
                    Header = new byte[Constants.HEADER_SIZE],
                    Data = new byte[_max_PacketSize]
                };

                ClientReceiveHeaderAsync(state);
            }
            catch
            {
                /* IGNORE */
            }

            Listen();
        }

        private void ClientReceiveHeaderAsync(ServerClientStateObject state)
        {
            try
            {
                state.Socket.BeginReceive(
                    state.Header, 0, Constants.HEADER_SIZE, SocketFlags.None, ClientReceiveHeaderCallback, state);
            }
            catch { InvokeClientDisconnected(state.Socket); }
        }

        private void ClientReceiveHeaderCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;
            try
            {
                if (state.Socket.EndReceive(iar) <= 0)
                {
                    InvokeClientDisconnected(state.Socket);
                    return;
                }

                state.Header.GetHeader(out state.CommandID, out state.DataLength, out state.ResponseID);

                if (state.DataLength > 0)
                {
                    state.Socket.BeginReceive(
                        state.Data, 0, state.DataLength, SocketFlags.None, ClientReceiveDataCallback, state);
                    return;
                }

                ClientReceiveHeaderAsync(state);
            }
            catch
            {
                InvokeClientDisconnected(state.Socket);
            }
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
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

            uint commandID = state.CommandID;
            int dataLenght = state.DataLength;
            uint responseID = state.ResponseID;
            Socket socket = state.Socket;

            byte[] data = ByteArrayPool.Rent(dataLenght);
            Buffer.BlockCopy(state.Data, 0, data, 0, dataLenght);

            ClientReceiveHeaderAsync(state);

            if (length == dataLenght)
            {
                DeserializeDataAsync(socket, commandID, data, 0, dataLenght, responseID);
                ByteArrayPool.Return(data);
            }
        }

        #endregion

        #region Nested

        private sealed class ServerClientStateObject
        {
            #region Variables

            public uint CommandID;
            public byte[] Data;
            public int DataLength;
            public byte[] Header;
            public uint ResponseID;
            public Socket Socket;

            #endregion
        }

        #endregion
    }
}