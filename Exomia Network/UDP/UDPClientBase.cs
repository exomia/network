using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;

namespace Exomia.Network.UDP
{
    /// <inheritdoc />
    public abstract class UdpClientBase : ClientBase
    {
        #region Variables

        private readonly byte[] _connectChecksum = new byte[16];

        private readonly ManualResetEvent _manuelResetEvent;

        private readonly ClientStateObject _state;

        #endregion

        #region Constructors

        /// <inheritdoc />
        protected UdpClientBase()
        {
            _state = new ClientStateObject { Buffer = new byte[Constants.PACKET_SIZE_MAX] };

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

        #region Methods

        /// <inheritdoc />
        protected override bool OnConnect(string serverAddress, int port, int timeout, out Socket socket)
        {
            _manuelResetEvent.Reset();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Connect(Dns.GetHostAddresses(serverAddress), port);
                ClientReceiveDataAsync();
                SendConnect();

                return _manuelResetEvent.WaitOne(timeout * 1000);
            }
            catch
            {
                /* IGNORE */
            }

            socket = null;
            return false;
        }

        private void ClientReceiveDataAsync()
        {
            try
            {
                _clientSocket.BeginReceive(
                    _state.Buffer, 0, Constants.PACKET_SIZE_MAX, SocketFlags.None, ClientReceiveDataCallback, null);
            }
            catch { OnDisconnected(); }
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
        {
            int length;
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

            _state.Buffer.GetHeader(out uint commandID, out uint type, out int dataLength);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                byte[] data = ByteArrayPool.Rent(dataLength);
                Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                DeserializeDataAsync(commandID, type, data, dataLength);
                ByteArrayPool.Return(data);
            }

            ClientReceiveDataAsync();
        }

        private void SendConnect()
        {
            UDP_CONNECT_STRUCT connect = new UDP_CONNECT_STRUCT { Checksum = _connectChecksum };
            base.SendData<UDP_CONNECT_STRUCT>(
                Constants.UDP_CONNECT_COMMAND_ID, Constants.UDP_CONNECT_STRUCT_TYPE_ID, connect);
        }

        #endregion

        #region Nested

        private struct ClientStateObject
        {
            public byte[] Buffer;
        }

        #endregion
    }
}