using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Serialization;

namespace Exomia.Network.TCP
{
    internal sealed class ServerClientStateObject
    {
        public uint CommandID;
        public byte[] Data;
        public uint DataLength;
        public byte[] Header;
        public Socket Socket;
        public uint Type;
    }

    /// <inheritdoc />
    public abstract class TcpServerBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        #region Constants

        #endregion

        #region Variables

        #region Statics

        #endregion

        #endregion

        #region Properties

        #region Statics

        #endregion

        #endregion

        #region Constructors

        #region Statics

        #endregion

        /// <summary>
        ///     TCPServerBase constructor
        /// </summary>
        public TcpServerBase() { }

        /// <summary>
        ///     TCPServerBase constructor
        /// </summary>
        /// <param name="max_data_size">max_data_size</param>
        public TcpServerBase(int max_data_size)
            : base(max_data_size) { }

        #endregion

        #region Methods

        #region Statics

        #endregion

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(localEndPoint);
                listener.Listen(100);

                Listen();
                return true;
            }
            catch { }

            listener = null;
            return false;
        }

        private void Listen()
        {
            try
            {
                _listener.BeginAccept(AcceptCallback, _listener);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;
            try
            {
                Socket socket = listener.EndAccept(ar);

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

                state.Header.GetHeaderInfo(out state.CommandID, out state.Type, out state.DataLength);

                if (state.DataLength > 0)
                {
                    state.Socket.BeginReceive(
                        state.Data, 0, (int)state.DataLength, SocketFlags.None, ClientReceiveDataCallback, state);
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
            int length = 0;
            try
            {
                if ((length = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnected(state.Socket);
                    return;
                }
            }
            catch { InvokeClientDisconnected(state.Socket); }

            uint type = state.Type;
            uint commandID = state.CommandID;
            uint dataLenght = state.DataLength;
            Socket socket = state.Socket;

            byte[] data = new byte[state.DataLength];
            Buffer.BlockCopy(state.Data, 0, data, 0, data.Length);

            ClientReceiveHeaderAsync(state);

            if (length == dataLenght)
            {
                DeserializeDataAsync(socket, commandID, type, data);
            }
        }

        /// <inheritdoc />
        protected override void BeginSendDataTo(Socket arg0, byte[] send)
        {
            if (arg0 == null) { return; }

            try
            {
                arg0.BeginSend(send, 0, send.Length, SocketFlags.None, SendDataCallback, arg0);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private static void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                Socket sender = (Socket)iar.AsyncState;
                sender.EndSend(iar);
            }
            catch { }
        }

        #endregion
    }
}