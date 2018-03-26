using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Serialization;

namespace Exomia.Network.TCP
{
    internal struct ClientStateObject
    {
        public byte[] Header;
        public byte[] Data;
        public uint CommandID;
        public uint Type;
        public uint DataLength;
    }

    /// <inheritdoc />
    public abstract class TcpClientBase : ClientBase
    {
        #region Variables

        #region Statics

        #endregion

        private readonly int _max_data_size = Constants.PACKET_SIZE_MAX;

        #endregion

        #region Constants

        #endregion

        #region Properties

        #region Statics

        #endregion

        #endregion

        #region Constructors

        #region Statics

        #endregion

        /// <inheritdoc />
        protected TcpClientBase() { }

        /// <inheritdoc />
        protected TcpClientBase(int maxDataSize)
        {
            if (maxDataSize <= 0)
            {
                maxDataSize = Constants.PACKET_SIZE_MAX;
            }
            _max_data_size = maxDataSize;
        }

        #endregion

        #region Methods

        #region Statics

        #endregion

        /// <inheritdoc />
        protected override bool OnConnect(string serverAddress, int port, int timeout, out Socket socket)
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    Blocking = false
                };

                IAsyncResult iar = socket.BeginConnect(Dns.GetHostAddresses(serverAddress), port, null, null);
                bool result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                socket.EndConnect(iar);
                if (result)
                {
                    ReceiveHeaderAsync();
                    return true;
                }
            }
            catch { }
            socket = null;
            return false;
        }

        private void ReceiveHeaderAsync()
        {
            ClientStateObject state = new ClientStateObject
            {
                Header = new byte[Constants.HEADER_SIZE],
                Data = new byte[_max_data_size]
            };
            try
            {
                _clientSocket.BeginReceive(
                    state.Header, 0, Constants.HEADER_SIZE, SocketFlags.None, ReceiveHeaderCallback, state);
            }
            catch { OnDisconnected(); }
        }

        private void ReceiveHeaderCallback(IAsyncResult iar)
        {
            try
            {
                if (_clientSocket.EndReceive(iar) <= 0)
                {
                    OnDisconnected();
                    return;
                }
            }
            catch
            {
                OnDisconnected();
                return;
            }

            ClientStateObject state = (ClientStateObject)iar.AsyncState;
            state.Header.GetHeaderInfo(out state.CommandID, out state.Type, out state.DataLength);

            if (state.DataLength > 0)
            {
                _clientSocket.BeginReceive(
                    state.Data, 0, (int)state.DataLength, SocketFlags.None, ClientReceiveDataCallback, state);
                return;
            }

            ReceiveHeaderAsync();
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
        {
            int length = 0;
            try
            {
                if ((length = _clientSocket.EndReceive(iar)) <= 0)
                {
                    OnDisconnected();
                    return;
                }
            }
            catch
            {
                OnDisconnected();
                return;
            }

            ClientStateObject state = (ClientStateObject)iar.AsyncState;
            uint type = state.Type;
            uint commandID = state.CommandID;
            uint dataLenght = state.DataLength;

            byte[] data = new byte[state.DataLength];
            Buffer.BlockCopy(state.Data, 0, data, 0, data.Length);

            ReceiveHeaderAsync();

            if (length == dataLenght)
            {
                DeserializeDataAsync(commandID, type, data);
            }
        }

        #endregion
    }
}