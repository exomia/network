#pragma warning disable 1574

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <inheritdoc />
    public abstract class ClientBase : IClient, IDisposable
    {
        #region Variables

        private const int INITIAL_QUEUE_SIZE = 16;

        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        /// <summary>
        ///     Socket
        /// </summary>
        protected Socket _clientSocket;

        private int _port;
        private string _serverAddress;

        #endregion

        #region Properties

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

        /// <inheritdoc />
        public bool Connect(string serverAddress, int port, int timeout = 10)
        {
            if (_clientSocket != null) { return true; }

            _serverAddress = serverAddress;
            _port = port;

            return OnConnect(serverAddress, port, timeout, out _clientSocket);
        }

        /// <summary>
        ///     called than the client is Disconnected
        /// </summary>
        public event DisconnectedHandler Disconnected;

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
        /// <param name="length">data length</param>
        protected async void DeserializeDataAsync(uint commandid, uint type, byte[] data, int length)
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
                            PING_STRUCT pingStruct = *(PING_STRUCT*)ptr;
                            result = pingStruct;
                        }
                    }
                    break;
                }
                case Constants.UDP_CONNECT_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out UDP_CONNECT_STRUCT connectStruct);
                    result = connectStruct;
                    break;
                }

                case Constants.CLIENTINFO_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out CLIENTINFO_STRUCT clientinfoStruct);
                    result = clientinfoStruct;
                    break;
                }
            }

            if (result == null)
            {
                result = await Task.Run(delegate { return DeserializeData(type, data, length); });
                if (result == null) { return; }
            }

            buffer.RaiseAsync(this, result);
        }

        /// <summary>
        ///     deserialize data from type and byte array
        /// </summary>
        /// <param name="type">type</param>
        /// <param name="data">byte array</param>
        /// <param name="length">data length</param>
        /// <returns>a new created object</returns>
        protected abstract object DeserializeData(uint type, byte[] data, int length);

        /// <summary>
        ///     OnDisconnected called if the client is disconnected
        /// </summary>
        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

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

        #region Send

        /// <inheritdoc />
        public void SendData(uint commandid, uint type, byte[] data, int lenght)
        {
            BeginSendData(commandid, type, data, lenght);
        }

        /// <inheritdoc />
        public void SendData(uint commandid, uint type, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize();
            BeginSendData(commandid, type, dataB, dataB.Length);
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
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            BeginSendData(commandid, type, dataB, lenght);
        }

        /// <inheritdoc />
        public void SendDataAsync<T>(uint commandid, uint type, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            Task.Run(
                delegate
                {
                    BeginSendData(commandid, type, dataB, lenght);
                });
        }

        private void BeginSendData(uint commandid, uint type, byte[] data, int lenght)
        {
            if (_clientSocket == null) { return; }

            byte[] send = Serialization.Serialization.Serialize(commandid, type, data, lenght);

            try
            {
                _clientSocket.BeginSend(
                    send, 0, Constants.HEADER_SIZE + lenght, SocketFlags.None, SendDataCallback, send);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                _clientSocket.EndSend(iar);
                byte[] send = (byte[])iar.AsyncState;
                ByteArrayPool.Return(send);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        public void SendPing()
        {
            PING_STRUCT pingStruct = new PING_STRUCT { TimeStamp = DateTime.Now.Ticks };
            SendData(Constants.PING_COMMAND_ID, Constants.PING_STRUCT_TYPE_ID, pingStruct);
        }

        /// <inheritdoc />
        public void SendClientInfo(long clientID, string clientName)
        {
            CLIENTINFO_STRUCT clientinfo = new CLIENTINFO_STRUCT { ClientID = clientID, ClientName = clientName };
            SendData(Constants.CLIENTINFO_COMMAND_ID, Constants.CLIENTINFO_STRUCT_TYPE_ID, clientinfo);
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