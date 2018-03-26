using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Serialization;

namespace Exomia.Network.UDP
{
    internal sealed class ServerClientStateObject
    {
        public byte[] Buffer;
        public EndPoint EndPoint;
    }

    /// <inheritdoc />
    public abstract class UdpServerBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        #region Variables

        #region Statics

        #endregion

        /// <summary>
        ///     _max_idle_time
        /// </summary>
        protected double _max_idle_time = Constants.UDP_IDLE_TIME;

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
        protected UdpServerBase() { }

        /// <inheritdoc />
        protected UdpServerBase(int maxPacketSize)
            : base(maxPacketSize) { }

        /// <inheritdoc />
        /// <summary>
        ///     UDPServerBase constructor
        /// </summary>
        /// <param name="maxIdleTime">maxIdleTime</param>
        protected UdpServerBase(double maxIdleTime)
            : this(Constants.PACKET_SIZE_MAX, maxIdleTime) { }

        /// <inheritdoc />
        /// <summary>
        ///     UDPServerBase constructor
        /// </summary>
        /// <param name="maxPacketSize">maxPacketSize</param>
        /// <param name="maxIdleTime">maxIdleTime</param>
        protected UdpServerBase(int maxPacketSize, double maxIdleTime)
            : base(maxPacketSize)
        {
            if (maxIdleTime <= 0) { maxIdleTime = Constants.UDP_IDLE_TIME; }
            _max_idle_time = maxIdleTime;
        }

        #endregion

        #region Methods

        #region Statics

        #endregion

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(localEndPoint);

                Listen();
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
        }

        private void Listen()
        {
            ServerClientStateObject state = new ServerClientStateObject
            {
                Buffer = new byte[_max_PacketSize],
                EndPoint = new IPEndPoint(IPAddress.Any, 0)
            };
            try
            {
                _listener.BeginReceiveFrom(
                    state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref state.EndPoint,
                    ClientReceiveDataCallback, state);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;
            int length = 0;
            try
            {
                if ((length = _listener.EndReceiveFrom(iar, ref state.EndPoint)) <= 0)
                {
                    InvokeClientDisconnected(state.EndPoint);
                    return;
                }
            }
            catch
            {
                InvokeClientDisconnected(state.EndPoint);
                return;
            }
            finally
            {
                Listen();
            }

            state.Buffer.GetHeaderInfo(out uint command_id, out uint type, out uint data_length);

            if (data_length == length - Constants.HEADER_SIZE)
            {
                byte[] data = new byte[data_length];
                Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE, data, 0, data.Length);
                DeserializeDataAsync(state.EndPoint, command_id, type, data);
            }
        }

        /// <inheritdoc />
        protected override void BeginSendDataTo(EndPoint arg0, byte[] send)
        {
            try
            {
                _listener.BeginSendTo(send, 0, send.Length, SocketFlags.None, arg0, SendDataToCallback, _listener);
            }
            catch { }
        }

        private static void SendDataToCallback(IAsyncResult iar)
        {
            try
            {
                Socket socket = (Socket)iar.AsyncState;
                socket.EndSendTo(iar);
            }
            catch
            {
                /* IGNORE */
            }
        }

        #endregion
    }
}