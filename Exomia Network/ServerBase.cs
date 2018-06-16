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

#pragma warning disable 1574

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <inheritdoc cref="IServer{T}" />
    public abstract class ServerBase<T, TServerClient> : IServer<T>, IDisposable
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        #region Variables

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_CLIENT_QUEUE_SIZE = 32;

        /// <summary>
        /// </summary>
        protected const int CLOSE_TIMEOUT = 10;

        /// <summary>
        /// </summary>
        protected const byte RECEIVE_FLAG = 0b0000_0001;

        /// <summary>
        /// </summary>
        protected const byte SEND_FLAG = 0b0000_0010;

        /// <summary>
        ///     called than a client is connected
        /// </summary>
        public event ClientActionHandler<T> ClientConnected;

        /// <summary>
        ///     called than a client is disconnected
        /// </summary>
        public event ClientDisconnectHandler<T> ClientDisconnected;

        /// <summary>
        ///     Dictionary{EndPoint, TServerClient}
        /// </summary>
        protected readonly Dictionary<T, TServerClient> _clients;

        private readonly Dictionary<uint, ServerClientEventEntry<T, TServerClient>> _dataReceivedCallbacks;

        private SpinLock _clientsLock;
        private SpinLock _dataReceivedCallbacksLock;

        private bool _isRunning;

        /// <summary>
        ///     Socket
        /// </summary>
        protected Socket _listener;

        /// <summary>
        ///     port
        /// </summary>
        protected int _port;

        /// <summary>
        ///     state
        /// </summary>
        protected byte _state;

        #endregion

        #region Properties

        /// <summary>
        ///     Port
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        #endregion

        #region Constructors

        /// <summary>
        ///     ServerBase constructor
        /// </summary>
        protected ServerBase()
        {
            _dataReceivedCallbacks = new Dictionary<uint, ServerClientEventEntry<T, TServerClient>>(INITIAL_QUEUE_SIZE);
            _clients = new Dictionary<T, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);

            _clientsLock = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
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

        /// <inheritdoc />
        public bool Run(int port)
        {
            if (_isRunning) { return true; }
            _isRunning = true;
            _port = port;

            if (OnRun(port, out _listener))
            {
                _state = RECEIVE_FLAG | SEND_FLAG;
                ListenAsync();
                return true;
            }
            return false;
        }

        /// <summary>
        ///     called than a server wants to run
        /// </summary>
        /// <param name="port">port</param>
        /// <param name="listener">out socket</param>
        /// <returns></returns>
        protected abstract bool OnRun(int port, out Socket listener);

        /// <summary>
        /// </summary>
        protected abstract void ListenAsync();

        /// <summary>
        ///     call to deserialize the data async
        /// </summary>
        /// <param name="arg0">Soicket|Endpoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">data length</param>
        /// <param name="responseid">responseid</param>
        protected void DeserializeData(T arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            switch (commandid)
            {
                case CommandID.PING:
                {
                    SendTo(arg0, CommandID.PING, data, offset, length, responseid);
                }
                    break;
                case CommandID.CONNECT:
                {
                    InvokeClientConnected(arg0);
                    SendTo(arg0, CommandID.CONNECT, data, offset, length, responseid);
                }
                    break;
                case CommandID.CLIENTINFO:
                {
                    if (_clients.TryGetValue(arg0, out TServerClient sClient))
                    {
                        data.FromBytesUnsafe(out CLIENTINFO_STRUCT clientinfoStruct);
                        sClient.SetClientInfo(clientinfoStruct);
                    }
                }
                    break;
                case CommandID.DISCONNECT:
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Graceful);
                }
                    break;
                default:
                {
                    if (_clients.TryGetValue(arg0, out TServerClient sClient))
                    {
                        if (commandid <= Constants.USER_COMMAND_LIMIT && _dataReceivedCallbacks.TryGetValue(
                                commandid, out ServerClientEventEntry<T, TServerClient> scee))
                        {
                            sClient.SetLastReceivedPacketTimeStamp();

                            Packet packet = new Packet(data, offset, length);
                            scee._deserialize.BeginInvoke(
                                in packet, iar =>
                                {
                                    object res = scee._deserialize.EndInvoke(in packet, iar);
                                    ByteArrayPool.Return(data);

                                    if (res != null) { scee.RaiseAsync(this, arg0, res, responseid, sClient); }
                                }, null);
                            return;
                        }
                    }
                }
                    break;
            }
            ByteArrayPool.Return(data);
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
        /// <param name="reason">DisconnectReason</param>
        protected void InvokeClientDisconnect(T arg0, DisconnectReason reason)
        {
            bool lockTaken = false;
            bool removed;
            try
            {
                _clientsLock.Enter(ref lockTaken);
                removed = _clients.Remove(arg0);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }

            if (removed)
            {
                OnClientDisconnected(arg0, reason);
                ClientDisconnected?.Invoke(arg0, reason);
            }

            OnAfterClientDisconnect(arg0);
        }

        /// <summary>
        ///     called then the client is connected
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="reason">DisconnectReason</param>
        protected virtual void OnClientDisconnected(T arg0, DisconnectReason reason) { }

        /// <summary>
        ///     called after <see cref="InvokeClientDisconnect" />.
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        protected virtual void OnAfterClientDisconnect(T arg0) { }

        private void InvokeClientConnected(T arg0)
        {
            if (CreateServerClient(arg0, out TServerClient serverClient))
            {
                bool lockTaken = false;
                try
                {
                    _clientsLock.Enter(ref lockTaken);
                    _clients.Add(arg0, serverClient);
                }
                finally
                {
                    if (lockTaken) { _clientsLock.Exit(false); }
                }

                OnClientConnected(arg0);
                ClientConnected?.Invoke(arg0);
            }
        }

        #endregion

        #region Add & Remove

        /// <summary>
        ///     add a command
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="deserialize"></param>
        public void AddCommand(uint commandid, DeserializePacket<object> deserialize)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (deserialize == null) { throw new ArgumentNullException(nameof(deserialize)); }

            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                if (!_dataReceivedCallbacks.TryGetValue(commandid, out ServerClientEventEntry<T, TServerClient> buffer))
                {
                    buffer = new ServerClientEventEntry<T, TServerClient>(deserialize);
                    _dataReceivedCallbacks.Add(commandid, buffer);
                }
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }
        }

        /// <summary>
        ///     remove a command
        /// </summary>
        /// <param name="commandid">command id</param>
        public bool RemoveCommand(uint commandid)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                return _dataReceivedCallbacks.Remove(commandid);
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }
        }

        /// <summary>
        ///     add a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">ClientDataReceivedHandler{Socket|Endpoint}</param>
        public void AddDataReceivedCallback(uint commandid, ClientDataReceivedHandler<T, TServerClient> callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            ServerClientEventEntry<T, TServerClient> buffer;
            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                if (!_dataReceivedCallbacks.TryGetValue(commandid, out buffer))
                {
                    throw new Exception(
                        $"Invalid paramater '{nameof(commandid)}'! Use 'AddCommand(uint, DeserializeData)' first.");
                }
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }

            buffer.Add(callback);
        }

        /// <summary>
        ///     remove a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">ClientDataReceivedHandler{Socket|Endpoint}</param>
        public void RemoveDataReceivedCallback(uint commandid, ClientDataReceivedHandler<T, TServerClient> callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            if (_dataReceivedCallbacks.TryGetValue(commandid, out ServerClientEventEntry<T, TServerClient> buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        #region Send

        /// <inheritdoc />
        public void SendTo(T arg0, uint commandid, byte[] data, int offset, int lenght, uint responseid)
        {
            BeginSendDataTo(arg0, commandid, data, offset, lenght, responseid);
        }

        /// <inheritdoc />
        public void SendTo(T arg0, uint commandid, ISerializable serializable, uint responseid)
        {
            byte[] dataB = serializable.Serialize(out int length);
            BeginSendDataTo(arg0, commandid, dataB, 0, length, responseid);
        }

        /// <inheritdoc />
        public void SendTo<T1>(T arg0, uint commandid, in T1 data, uint responseid) where T1 : struct
        {
            byte[] dataB = data.ToBytesUnsafe(out int length);
            BeginSendDataTo(arg0, commandid, dataB, 0, length, responseid);
        }

        private void BeginSendDataTo(T arg0, uint commandid, byte[] data, int offset, int length, uint responseid)
        {
            if (_listener == null) { return; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.Serialize(
                    commandid, data, offset, length, responseid, EncryptionMode.None, out byte[] send, out int size);
                BeginSendDataTo(arg0, send, 0, size);
            }
        }

        /// <summary>
        ///     send the data to the client
        ///     its only called if the SEND_FLAG is set in the _state variable
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="send">data to send</param>
        /// <param name="offset">offset</param>
        /// <param name="length">data length</param>
        protected abstract void BeginSendDataTo(T arg0, byte[] send, int offset, int length);

        /// <inheritdoc />
        public void SendToAll(uint commandid, byte[] data, int offset, int length)
        {
            Dictionary<T, TServerClient> buffer;
            lock (_clients)
            {
                buffer = new Dictionary<T, TServerClient>(_clients);
            }

            if (buffer.Count > 0)
            {
                foreach (T endPoint in buffer.Keys)
                {
                    SendTo(endPoint, commandid, data, offset, length, 0);
                }
            }
        }

        /// <inheritdoc />
        public void SendToAll<T1>(uint commandid, in T1 data) where T1 : struct
        {
            byte[] dataB = data.ToBytesUnsafe(out int length);
            SendToAll(commandid, dataB, 0, length);
        }

        /// <inheritdoc />
        public void SendToAll(uint commandid, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize(out int length);
            SendToAll(commandid, dataB, 0, length);
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
                        _listener?.Shutdown(SocketShutdown.Both);
                        _listener?.Close(CLOSE_TIMEOUT);
                    }
                    catch
                    {
                        /* IGNORE */
                    }
                    _listener = null;
                }

                _state = 0;

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