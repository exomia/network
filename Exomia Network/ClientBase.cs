#pragma warning disable 1574

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <inheritdoc />
    public abstract class ClientBase : IClient, IDisposable
    {
        #region Constants

        private const int INITIAL_QUEUE_SIZE = 16;

        #endregion

        #region Variables

        #region Statics

        #endregion

        /// <summary>
        ///     called than the client is Disconnected
        /// </summary>
        public event DisconnectedHandler Disconnected;

        private int _port;
        private string _serverAddress;

        /// <summary>
        ///     Socket
        /// </summary>
        protected Socket _clientSocket;

        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        #endregion

        #region Properties

        #region Statics

        #endregion

        /// <summary>
        ///     Port
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        ///     ServerAddress
        /// </summary>
        public string ServerAddress
        {
            get { return _serverAddress; }
        }

        #endregion

        #region Constructors

        #region Statics

        #endregion

        /// <summary>
        ///     ClientBase constructor
        /// </summary>
        protected ClientBase()
        {
            _clientSocket = null;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
        }

        /// <summary>
        ///     ClientBase destructor
        /// </summary>
        ~ClientBase()
        {
            Dispose(false);
        }

        #endregion

        #region Methods

        #region Statics

        #endregion

        #region Add & Remove

        /// <summary>
        ///     remove a command
        /// </summary>
        /// <param name="commandid">command id</param>
        public void RemoveCommand(uint commandid)
        {
            if (_dataReceivedCallbacks.ContainsKey(commandid))
            {
                _dataReceivedCallbacks.Remove(commandid);
            }
        }

        /// <summary>
        ///     add a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">callback</param>
        public void AddDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                buffer.Add(callback);
            }
            else
            {
                ClientEventEntry entry = new ClientEventEntry();
                entry.Add(callback);
                _dataReceivedCallbacks.Add(commandid, entry);
            }
        }

        /// <summary>
        ///     remove a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">DataReceivedHandler</param>
        public void RemoveDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        /// <inheritdoc />
        public bool Connect(string serverAddress, int port, int timeout = 10)
        {
            if (_clientSocket != null) { return true; }

            _serverAddress = serverAddress;
            _port = port;

            return OnConnect(serverAddress, port, timeout, out _clientSocket);
        }

        /// <summary>
        ///     called than a client wants to connect with a server
        /// </summary>
        /// <param name="serverAddress">serverAddress</param>
        /// <param name="port">port</param>
        /// <param name="timeout">timeout</param>
        /// <param name="socket">out socket</param>
        /// <returns></returns>
        protected abstract bool OnConnect(string serverAddress, int port, int timeout, out Socket socket);

        /// <summary>
        ///     call to deserialize the data async
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        protected async void DeserializeDataAsync(uint commandid, uint type, byte[] data)
        {
            if (!_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                return;
            }

            object result = null;
            switch (commandid)
            {
                case Constants.PING_COMMAND_ID:
                {
                    unsafe
                    {
                        fixed (byte* ptr = data)
                        {
                            PING_STRUCT ping_struct = *(PING_STRUCT*)ptr;
                            result = ping_struct;
                        }
                    }
                    break;
                }
                case Constants.UDP_CONNECT_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out UDP_CONNECT_STRUCT connect_struct);
                    result = connect_struct;
                    break;
                }

                case Constants.CLIENTINFO_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out CLIENTINFO_STRUCT clientinfo_struct);
                    result = clientinfo_struct;
                    break;
                }
            }

            if (result == null)
            {
                result = await Task.Run(delegate { return DeserializeData(type, data); });
                if (result == null) { return; }
            }

            buffer.RaiseAsync(this, result);
        }

        /// <summary>
        ///     deserialize data from type and byte array
        /// </summary>
        /// <param name="type">type</param>
        /// <param name="data">byte array</param>
        /// <returns>a new created object</returns>
        protected abstract object DeserializeData(uint type, byte[] data);

        #region Send

        /// <inheritdoc />
        public void SendData(uint commandid, uint type, byte[] data)
        {
            BeginSendData(commandid, type, data);
        }

        /// <inheritdoc />
        public void SendData(uint commandid, uint type, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize();
            BeginSendData(commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendDataAsync(uint commandid, uint type, ISerializable serializable)
        {
            Task.Run(
                delegate
                {
                    SendData(commandid, type, serializable);
                });
        }

        /// <inheritdoc />
        public void SendData<T>(uint commandid, uint type, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB);
            BeginSendData(commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendDataAsync<T>(uint commandid, uint type, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB);
            Task.Run(
                delegate
                {
                    BeginSendData(commandid, type, dataB);
                });
        }

        private void BeginSendData(uint commandid, uint type, byte[] data)
        {
            if (_clientSocket == null) { return; }

            byte[] send = Serialization.Serialization.Serialize(commandid, type, data);

            try
            {
                _clientSocket.BeginSend(send, 0, send.Length, SocketFlags.None, SendDataCallback, _clientSocket);
            }
            catch { }
        }

        private static void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                Socket sender = (Socket)iar.AsyncState;
                sender.EndSend(iar);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        public void SendPing()
        {
            PING_STRUCT ping_struct = new PING_STRUCT { TimeStamp = DateTime.Now.Ticks };
            SendData(Constants.PING_COMMAND_ID, Constants.PING_STRUCT_TYPE_ID, ping_struct);
        }

        /// <inheritdoc />
        public void SendClientInfo(long clientID, string clientName)
        {
            CLIENTINFO_STRUCT clientinfo = new CLIENTINFO_STRUCT { ClientID = clientID, ClientName = clientName };
            SendData<CLIENTINFO_STRUCT>(
                Constants.CLIENTINFO_COMMAND_ID, Constants.CLIENTINFO_STRUCT_TYPE_ID, clientinfo);
        }

        #endregion

        /// <summary>
        ///     OnDisconnected called if the client is disconnected
        /// </summary>
        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        #endregion

        #region IDisposable Support

        private bool _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                OnDispose(disposing);
                if (disposing)
                {
                    /* USER CODE */
                    try
                    {
                        _clientSocket?.Shutdown(SocketShutdown.Both);
                        _clientSocket?.Close(5000);
                    }
                    catch
                    {
                        /* IGNORE */
                    }
                    _clientSocket = null;
                }
                _disposed = true;
            }
        }

        /// <summary>
        ///     OnDispose
        /// </summary>
        /// <param name="disposing">disposing</param>
        protected virtual void OnDispose(bool disposing) { }

        #endregion
    }
}