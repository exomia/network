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
    public abstract class ServerBase<T, TServerClient> : IServer<T>, IDisposable
        where T : class
        where TServerClient : ServerClientBase<T>
    {
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

        #endregion

        #region Constants

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_CLIENT_QUEUE_SIZE = 128;

        #endregion

        #region Variables

        #region Statics

        #endregion

        /// <summary>
        ///     called than a client is connected
        /// </summary>
        public event ClientActionHandler<T> ClientConnected;

        /// <summary>
        ///     called than a client is disconnected
        /// </summary>
        public event ClientActionHandler<T> ClientDisconnected;

        private bool _isRunning;
        private int _port;

        /// <summary>
        ///     _max_PacketSize
        /// </summary>
        protected int _max_PacketSize = Constants.PACKET_SIZE_MAX;

        /// <summary>
        ///     Socket
        /// </summary>
        protected Socket _listener;

        private readonly Dictionary<uint, ServerClientEventEntry<T, TServerClient>> _dataReceivedCallbacks;

        /// <summary>
        ///     Dictionary{EndPoint, TServerClient}
        /// </summary>
        protected readonly Dictionary<T, TServerClient> _clients;

        #endregion

        #region Constructors

        #region Statics

        #endregion

        /// <summary>
        ///     ServerBase constructor
        /// </summary>
        protected ServerBase()
        {
            _dataReceivedCallbacks = new Dictionary<uint, ServerClientEventEntry<T, TServerClient>>(INITIAL_QUEUE_SIZE);
            _clients = new Dictionary<T, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);
        }

        /// <inheritdoc />
        /// <summary>
        ///     ServerBase constructor
        /// </summary>
        /// <param name="maxPacketSize">max_packet_size</param>
        protected ServerBase(int maxPacketSize)
            : this()
        {
            _max_PacketSize = maxPacketSize;
        }

        /// <summary>
        ///     ServerBase destuctor
        /// </summary>
        ~ServerBase()
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
        /// <param name="callback">ClientDataReceivedHandler{Socket}</param>
        public void AddDataReceivedCallback(uint commandid, ClientDataReceivedHandler<T, TServerClient> callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ServerClientEventEntry<T, TServerClient> buffer))
            {
                buffer.Add(callback);
            }
            else
            {
                ServerClientEventEntry<T, TServerClient> entry = new ServerClientEventEntry<T, TServerClient>();
                entry.Add(callback);
                _dataReceivedCallbacks.Add(commandid, entry);
            }
        }

        /// <summary>
        ///     remove a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">ClientDataReceivedHandler{Socket}</param>
        public void RemoveDataReceivedCallback(uint commandid, ClientDataReceivedHandler<T, TServerClient> callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ServerClientEventEntry<T, TServerClient> buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        /// <inheritdoc />
        public bool Run(int port)
        {
            if (_isRunning) { return true; }
            _isRunning = true;
            _port = port;

            return OnRun(port, out _listener);
        }

        /// <summary>
        ///     called than a server wants to run
        /// </summary>
        /// <param name="port">port</param>
        /// <param name="listener">out socket</param>
        /// <returns></returns>
        protected abstract bool OnRun(int port, out Socket listener);

        /// <summary>
        ///     call to deserialize the data async
        /// </summary>
        /// <param name="arg0">Soicket|Endpoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        protected async void DeserializeDataAsync(T arg0, uint commandid, uint type, byte[] data)
        {
            switch (commandid)
            {
                case Constants.PING_COMMAND_ID:
                {
                    SendDataTo(arg0, Constants.PING_COMMAND_ID, Constants.PING_STRUCT_TYPE_ID, data);
                    return;
                }
                case Constants.CLIENTINFO_COMMAND_ID:
                {
                    if (_clients.TryGetValue(arg0, out TServerClient sClient))
                    {
                        data.FromBytesUnsafe(out CLIENTINFO_STRUCT clientinfo_struct);
                        sClient.SetClientInfo(clientinfo_struct);
                    }
                    return;
                }
                case Constants.UDP_CONNECT_COMMAND_ID:
                {
                    InvokeClientConnected(arg0);
                    SendDataTo(arg0, Constants.UDP_CONNECT_COMMAND_ID, Constants.UDP_CONNECT_STRUCT_TYPE_ID, data);
                    return;
                }
            }

            if (_dataReceivedCallbacks.TryGetValue(commandid, out ServerClientEventEntry<T, TServerClient> buffer))
            {
                object result = await Task.Run(delegate { return DeserializeData(type, data); });
                if (result == null) { return; }

                buffer.RaiseAsync(this, arg0, result);
            }
        }

        /// <summary>
        ///     deserialize data from type and byte array
        /// </summary>
        /// <param name="type">type</param>
        /// <param name="data">byte array</param>
        /// <returns>a new created object</returns>
        protected abstract object DeserializeData(uint type, byte[] data);

        /// <summary>
        ///     needs to be called than a new client is connected
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        protected void InvokeClientConnected(T arg0)
        {
            if (CreateServerClient(arg0, out TServerClient serverClient))
            {
                _clients.Add(arg0, serverClient);
            }
            OnClientConnected(arg0);
            ClientConnected?.Invoke(arg0);
        }

        /// <summary>
        ///     called than a new client is connected
        /// </summary>
        /// <param name="arg0"></param>
        protected virtual void OnClientConnected(T arg0) { }

        /// <summary>
        ///     Create a new ServerClient than a client connects
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="serverClient">out new ServerClient</param>
        /// <returns><c>true</c> if the new ServerClient should be added to the clients list; <c>false</c> otherwise</returns>
        protected abstract bool CreateServerClient(T arg0, out TServerClient serverClient);

        /// <summary>
        ///     needs to be called than a client is disconnected
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        protected void InvokeClientDisconnected(T arg0)
        {
            OnClientDisconnected(arg0);
            ClientDisconnected?.Invoke(arg0);

            _clients.Remove(arg0);
        }

        /// <summary>
        ///     called then the client is connected
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        protected virtual void OnClientDisconnected(T arg0) { }

        #region Send

        /// <inheritdoc />
        public void SendDataTo(T arg0, uint commandid, uint type, byte[] data)
        {
            BeginSendDataTo(arg0, commandid, type, data);
        }

        /// <inheritdoc />
        public void SendDataTo(T arg0, uint commandid, uint type, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize();
            BeginSendDataTo(arg0, commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendDataToAsync(T arg0, uint commandid, uint type, ISerializable serializable)
        {
            Task.Run(
                delegate
                {
                    SendDataTo(arg0, commandid, type, serializable);
                });
        }

        /// <inheritdoc />
        public void SendDataTo<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct
        {
            byte[] dataB = data.ToBytesUnsafe();
            BeginSendDataTo(arg0, commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendDataToAsync<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct
        {
            data.ToBytesUnsafe(out byte[] dataB);
            Task.Run(
                delegate
                {
                    BeginSendDataTo(arg0, commandid, type, dataB);
                });
        }

        private void BeginSendDataTo(T arg0, uint commandid, uint type, byte[] data)
        {
            if (_listener == null) { return; }
            BeginSendDataTo(arg0, Serialization.Serialization.Serialize(commandid, type, data));
        }

        /// <summary>
        ///     send the data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="send">data to send</param>
        protected abstract void BeginSendDataTo(T arg0, byte[] send);

        /// <inheritdoc />
        public void SendToAll(uint commandid, uint type, byte[] data)
        {
            Dictionary<T, TServerClient> buffer = null;
            lock (_clients)
            {
                buffer = new Dictionary<T, TServerClient>(_clients);
            }

            if (buffer != null && buffer.Count > 0)
            {
                foreach (T endPoint in buffer.Keys)
                {
                    SendDataTo(endPoint, commandid, type, data);
                }
            }
        }

        /// <inheritdoc />
        public void SendToAllAsync(uint commandid, uint type, byte[] data)
        {
            Task.Run(
                delegate
                {
                    SendToAll(commandid, type, data);
                });
        }

        /// <inheritdoc />
        public void SendToAll<T1>(uint commandid, uint type, in T1 data) where T1 : struct
        {
            byte[] dataB = data.ToBytesUnsafe();
            SendToAll(commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendToAllAsync<T1>(uint commandid, uint type, in T1 data) where T1 : struct
        {
            byte[] dataB = data.ToBytesUnsafe();
            Task.Run(
                delegate
                {
                    SendToAll(commandid, type, dataB);
                });
        }

        /// <inheritdoc />
        public void SendToAll(uint commandid, uint type, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize();
            SendToAll(commandid, type, dataB);
        }

        /// <inheritdoc />
        public void SendToAllAsync(uint commandid, uint type, ISerializable serializable)
        {
            Task.Run(
                delegate
                {
                    SendToAll(commandid, type, serializable);
                });
        }

        #endregion

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
                        _listener?.Shutdown(SocketShutdown.Both);
                        _listener?.Close(5000);
                    }
                    catch
                    {
                        /* IGNORE */
                    }
                    _listener = null;
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