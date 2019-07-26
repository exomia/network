#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

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
    /// <summary>
    ///     A server base.
    /// </summary>
    /// <typeparam name="T">             Generic type parameter. </typeparam>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract class ServerBase<T, TServerClient> : IServer<T, TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        /// <summary>
        ///     The close timeout.
        /// </summary>
        private protected const int CLOSE_TIMEOUT = 10;

        /// <summary>
        ///     The receive flag.
        /// </summary>
        private protected const byte RECEIVE_FLAG = 0b0000_0001;

        /// <summary>
        ///     The send flag.
        /// </summary>
        private protected const byte SEND_FLAG = 0b0000_0010;

        /// <summary>
        ///     Initial size of the queue.
        /// </summary>
        private const int INITIAL_QUEUE_SIZE = 16;

        /// <summary>
        ///     Initial size of the client queue.
        /// </summary>
        private const int INITIAL_CLIENT_QUEUE_SIZE = 32;

        /// <summary>
        ///     called than a client is connected.
        /// </summary>
        public event ClientActionHandler<T> ClientConnected;

        /// <summary>
        ///     called than a client is disconnected.
        /// </summary>
        public event ClientDisconnectHandler<T> ClientDisconnected;

        /// <summary>
        ///     Dictionary{T, TServerClient}
        /// </summary>
        protected readonly Dictionary<T, TServerClient> _clients;

        /// <summary>
        ///     The listener.
        /// </summary>
        private protected Socket _listener;

        /// <summary>
        ///     The port.
        /// </summary>
        private protected int _port;

        /// <summary>
        ///     The state.
        /// </summary>
        private protected byte _state;

        /// <summary>
        ///     The data received callbacks.
        /// </summary>
        private readonly Dictionary<uint, ServerClientEventEntry<T, TServerClient>> _dataReceivedCallbacks;

        /// <summary>
        ///     The clients lock.
        /// </summary>
        private SpinLock _clientsLock;

        /// <summary>
        ///     The data received callbacks lock.
        /// </summary>
        private SpinLock _dataReceivedCallbacksLock;

        /// <summary>
        ///     True if this object is running.
        /// </summary>
        private bool _isRunning;

        /// <summary>
        ///     Port.
        /// </summary>
        /// <value>
        ///     The port.
        /// </value>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        ///     ServerBase constructor.
        /// </summary>
        private protected ServerBase()
        {
            _dataReceivedCallbacks = new Dictionary<uint, ServerClientEventEntry<T, TServerClient>>(INITIAL_QUEUE_SIZE);
            _clients               = new Dictionary<T, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);

            _clientsLock               = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
        }

        /// <summary>
        ///     ServerBase destructor.
        /// </summary>
        ~ServerBase()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public bool Run(int port)
        {
            if (_isRunning) { return true; }
            _isRunning = true;
            _port      = port;

            if (OnRun(port, out _listener))
            {
                _state = RECEIVE_FLAG | SEND_FLAG;
                ListenAsync();
                return true;
            }
            return false;
        }

        /// <summary>
        ///     Executes the run action.
        /// </summary>
        /// <param name="port">     Port. </param>
        /// <param name="listener"> [out] The listener. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private protected abstract bool OnRun(int port, out Socket listener);

        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
        private protected abstract void ListenAsync();

        /// <summary>
        ///     Deserialize data.
        /// </summary>
        /// <param name="arg0">       Socket|Endpoint. </param>
        /// <param name="commandid">  command id. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseid"> The responseid. </param>
        private protected void DeserializeData(T      arg0,
                                               uint   commandid,
                                               byte[] data,
                                               int    offset,
                                               int    length,
                                               uint   responseid)
        {
            switch (commandid)
            {
                case CommandID.PING:
                    {
                        SendTo(arg0, CommandID.PING, data, offset, length, responseid);
                        break;
                    }
                case CommandID.CONNECT:
                    {
                        InvokeClientConnected(arg0);
                        SendTo(arg0, CommandID.CONNECT, data, offset, length, responseid);
                        break;
                    }
                case CommandID.DISCONNECT:
                    {
                        InvokeClientDisconnect(arg0, DisconnectReason.Graceful);
                        break;
                    }
                default:
                    {
                        if (_clients.TryGetValue(arg0, out TServerClient sClient))
                        {
                            if (commandid <= Constants.USER_COMMAND_LIMIT &&
                                _dataReceivedCallbacks.TryGetValue(
                                    commandid, out ServerClientEventEntry<T, TServerClient> scee))
                            {
                                sClient.SetLastReceivedPacketTimeStamp();

                                Packet packet = new Packet(data, offset, length);
                                ThreadPool.QueueUserWorkItem(
                                    x =>
                                    {
                                        object res = scee._deserialize(in packet);
                                        ByteArrayPool.Return(data);

                                        if (res != null) { scee.Raise(this, sClient, res, responseid); }
                                    });
                                return;
                            }
                        }
                        break;
                    }
            }
            ByteArrayPool.Return(data);
        }

        /// <summary>
        ///     called than a new client is connected.
        /// </summary>
        /// <param name="arg0"> Socket|Endpoint. </param>
        protected virtual void OnClientConnected(T arg0) { }

        /// <summary>
        ///     Create a new ServerClient than a client connects.
        /// </summary>
        /// <param name="arg0">         Socket|EndPoint. </param>
        /// <param name="serverClient"> [out] out new ServerClient. </param>
        /// <returns>
        ///     <c>true</c> if the new ServerClient should be added to the clients list; <c>false</c>
        ///     otherwise.
        /// </returns>
        protected abstract bool CreateServerClient(T arg0, out TServerClient serverClient);

        /// <summary>
        ///     Executes the client disconnect on a different thread, and waits for the result.
        /// </summary>
        /// <param name="arg0">   Socket|Endpoint. </param>
        /// <param name="reason"> DisconnectReason. </param>
        private protected void InvokeClientDisconnect(T arg0, DisconnectReason reason)
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
        ///     called then the client is connected.
        /// </summary>
        /// <param name="arg0">   Socket|EndPoint. </param>
        /// <param name="reason"> DisconnectReason. </param>
        protected virtual void OnClientDisconnected(T arg0, DisconnectReason reason) { }

        /// <summary>
        ///     called after <see cref="InvokeClientDisconnect" />.
        /// </summary>
        /// <param name="arg0"> Socket|EndPoint. </param>
        protected virtual void OnAfterClientDisconnect(T arg0) { }

        /// <summary>
        ///     Executes the client connected on a different thread, and waits for the result.
        /// </summary>
        /// <param name="arg0"> Socket|Endpoint. </param>
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

        #region Add & Remove

        /// <summary>
        ///     add a command.
        /// </summary>
        /// <param name="commandid">   command id. </param>
        /// <param name="deserialize"> . </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when one or more required arguments
        ///     are null.
        /// </exception>
        public void AddCommand(uint commandid, DeserializePacketHandler<object> deserialize)
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
        ///     remove a command.
        /// </summary>
        /// <param name="commandid"> command id. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
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
        ///     add a data received callback.
        /// </summary>
        /// <param name="commandid"> command id. </param>
        /// <param name="callback">  ClientDataReceivedHandler{Socket|Endpoint} </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when one or more required arguments
        ///     are null.
        /// </exception>
        /// <exception cref="Exception">
        ///     Thrown when an exception error condition
        ///     occurs.
        /// </exception>
        public void AddDataReceivedCallback(uint commandid, ClientDataReceivedHandler<T, TServerClient> callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            ServerClientEventEntry<T, TServerClient> buffer;
            bool                                     lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                if (!_dataReceivedCallbacks.TryGetValue(commandid, out buffer))
                {
                    throw new Exception(
                        $"Invalid parameter '{nameof(commandid)}'! Use 'AddCommand(uint, DeserializeData)' first.");
                }
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }

            buffer.Add(callback);
        }

        /// <summary>
        ///     remove a data received callback.
        /// </summary>
        /// <param name="commandid"> command id. </param>
        /// <param name="callback">  ClientDataReceivedHandler{Socket|Endpoint} </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when one or more required arguments
        ///     are null.
        /// </exception>
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

        private protected abstract SendError SendTo(T      arg0,
                                                    uint   commandid,
                                                    byte[] data,
                                                    int    offset,
                                                    int    length,
                                                    uint   responseid);

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandid,
                                byte[]        data,
                                int           offset,
                                int           length,
                                uint          responseid)
        {
            return SendTo(client.Arg0, commandid, data, offset, length, responseid);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client, uint commandid, ISerializable serializable, uint responseid)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendTo(client.Arg0, commandid, dataB, 0, length, responseid);
        }

        /// <inheritdoc />
        public SendError SendTo<T1>(TServerClient client, uint commandid, in T1 data, uint responseid)
            where T1 : unmanaged
        {
            byte[] dataB = data.ToBytesUnsafe2(out int length);
            return SendTo(client.Arg0, commandid, dataB, 0, length, responseid);
        }

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
                foreach (T arg0 in buffer.Keys)
                {
                    SendTo(arg0, commandid, data, offset, length, 0);
                }
            }
        }

        /// <inheritdoc />
        public void SendToAll<T1>(uint commandid, in T1 data) where T1 : unmanaged
        {
            byte[] dataB = data.ToBytesUnsafe2(out int length);
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

        /// <summary>
        ///     True if disposed.
        /// </summary>
        private bool _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Releases the unmanaged resources used by the Exomia.Network.ServerBase&lt;T,
        ///     TServerClient&gt; and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"> disposing. </param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _state = 0;

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
                _disposed = true;
            }
        }

        /// <summary>
        ///     OnDispose.
        /// </summary>
        /// <param name="disposing"> disposing. </param>
        protected virtual void OnDispose(bool disposing) { }

        #endregion
    }
}