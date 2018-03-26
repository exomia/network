using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Serialization;

namespace Exomia.Network.UDP
{
    internal struct ClientStateObject
    {
        public byte[] Buffer;
    }

    /// <inheritdoc />
    public abstract class UdpClientBase : ClientBase
    {
        #region Constructors

        #region Statics

        #endregion

        /// <inheritdoc />
        protected UdpClientBase()
        {
            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);

            AddDataReceivedCallback(
                Constants.UDP_CONNECT_COMMAND_ID, (_, data) =>
                {
                    UDP_CONNECT_STRUCT connectStruct = (UDP_CONNECT_STRUCT)data;
                    if (connectStruct.Checksum.SequenceEqual(_connectChecksum))
                    {
                        _manuelResetEvent.Set();
                    }
                    return true;
                });
        }

        #endregion

        #region Constants

        #endregion

        #region Variables

        #region Statics

        #endregion

        private readonly byte[] _connectChecksum = new byte[16];

        private readonly ManualResetEvent _manuelResetEvent;

        #endregion

        #region Properties

        #region Statics

        #endregion

        #endregion

        #region Methods

        #region Statics

        #endregion

        /// <summary>
        ///     <see cref="ClientBase.OnConnect(string, int, int, out Socket)" />
        /// </summary>
        protected override bool OnConnect(string serverAddress, int port, int timeout, out Socket socket)
        {
            _manuelResetEvent.Reset();
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(Dns.GetHostAddresses(serverAddress), port);
                ClientReceiveDataAsync();
                SendConnect();

                return _manuelResetEvent.WaitOne(timeout * 1000);
            }
            catch { }

            socket = null;
            return false;
        }

        private void ClientReceiveDataAsync()
        {
            ClientStateObject state = new ClientStateObject
                { Buffer = new byte[Constants.PACKET_SIZE_MAX] };
            try
            {
                _clientSocket.BeginReceive(
                    state.Buffer, 0, Constants.PACKET_SIZE_MAX, SocketFlags.None, ClientReceiveDataCallback, state);
            }
            catch { OnDisconnected(); }
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

            ClientReceiveDataAsync();

            ClientStateObject state = (ClientStateObject)iar.AsyncState;
            state.Buffer.GetHeaderInfo(out uint command_id, out uint type, out uint data_length);

            if (data_length == length - Constants.HEADER_SIZE)
            {
                byte[] data = new byte[data_length];
                Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE, data, 0, data.Length);
                DeserializeDataAsync(command_id, type, data);
            }
        }

        private void SendConnect()
        {
            UDP_CONNECT_STRUCT connect = new UDP_CONNECT_STRUCT { Checksum = _connectChecksum };
            base.SendData<UDP_CONNECT_STRUCT>(
                Constants.UDP_CONNECT_COMMAND_ID, Constants.UDP_CONNECT_STRUCT_TYPE_ID, connect);
        }

        #endregion
    }
}