#region License

// Copyright (c) 2018-2020, exomia
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
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
#if NETSTANDARD2_1
using System.Diagnostics.CodeAnalysis;

#endif

namespace Exomia.Network
{
    /// <summary>
    ///     A server base.
    /// </summary>
    /// <typeparam name="T">             Socket|Endpoint. </typeparam>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract partial class ServerBase<T, TServerClient> : IServer<TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        private protected const int  CLOSE_TIMEOUT                      = 10;
        private protected const byte RECEIVE_FLAG                       = 0b0000_0001;
        private protected const byte SEND_FLAG                          = 0b0000_0010;
        private const           int  INITIAL_QUEUE_SIZE                 = 16;
        private const           int  INITIAL_CLIENT_QUEUE_SIZE          = 32;
        private const           int  INITIAL_TASK_COMPLETION_QUEUE_SIZE = 128;

        // ReSharper disable once StaticMemberInGenericType
        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     Called than a client is connected.
        /// </summary>
        public event ClientActionHandler<TServerClient>? ClientConnected;

        /// <summary>
        ///     Called than a client is disconnected.
        /// </summary>
        public event ClientDisconnectHandler<TServerClient>? ClientDisconnected;

        /// <summary>
        ///     Occurs when data from a client is received.
        /// </summary>
        public event ClientCommandDataReceivedHandler<TServerClient> ClientDataReceived
        {
            add { _clientDataReceived.Add(value); }
            remove { _clientDataReceived.Remove(value); }
        }

        /// <summary>
        ///     Gets the clients.
        /// </summary>
        /// <value>
        ///     The clients.
        /// </value>
        protected Dictionary<Guid, TServerClient>.ValueCollection Clients
        {
            get
            {
                Dictionary<Guid, TServerClient> clients;
                bool                            lockTaken = false;
                try
                {
                    _clientsLock.Enter(ref lockTaken);
                    clients = new Dictionary<Guid, TServerClient>(_clientGuids);
                }
                finally
                {
                    if (lockTaken) { _clientsLock.Exit(false); }
                }

                return clients.Values;
            }
        }

        private protected readonly Dictionary<T, TServerClient>    _clients;
        private protected readonly Dictionary<Guid, TServerClient> _clientGuids;
        private protected          Socket?                         _listener;
        private protected          int                             _port;
        private protected          byte                            _state;
        private                    ushort                          _requestID;

        /// <summary>
        ///     The compression mode.
        /// </summary>
        protected CompressionMode _compressionMode = CompressionMode.Lz4;

        private protected EncryptionMode _encryptionMode = EncryptionMode.None;
        private readonly  Dictionary<ushort, ServerClientEventEntry<TServerClient>> _dataReceivedCallbacks;
        private readonly  Event<ClientCommandDataReceivedHandler<TServerClient>> _clientDataReceived;

        private readonly Dictionary<ushort, TaskCompletionSource<(ushort requestID, Packet packet)>>
            _taskCompletionSources;

        private readonly byte     _listenerCount;
        private          SpinLock _clientsLock;
        private          SpinLock _dataReceivedCallbacksLock;
        private          SpinLock _lockTaskCompletionSources;
        private          bool     _isRunning;
        private          int      _packetID;

        /// <summary>
        ///     Gets the port.
        /// </summary>
        /// <value>
        ///     The port.
        /// </value>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        ///     Gets or sets the size of the receive buffer in bytes.
        /// </summary>
        /// <value>
        ///     The size of the receive buffer in bytes.
        /// </value>
        public int ReceiveBufferSize
        {
            get { return _listener?.ReceiveBufferSize ?? 0; }
            set
            {
                if (_listener != null)
                {
                    _listener.ReceiveBufferSize = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the size of the send buffer in bytes.
        /// </summary>
        /// <value>
        ///     The size of the send buffer in bytes.
        /// </value>
        public int SendBufferSize
        {
            get { return _listener?.SendBufferSize ?? 0; }
            set
            {
                if (_listener != null)
                {
                    _listener.ReceiveBufferSize = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the receive time out value of the connection in seconds.
        /// </summary>
        /// <value>
        ///     The receive time out value of the connection in seconds.
        /// </value>
        public int ReceiveTimeout
        {
            get { return _listener?.ReceiveTimeout ?? 0; }
            set
            {
                if (_listener != null)
                {
                    _listener.ReceiveTimeout = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the send time out value of the connection in seconds.
        /// </summary>
        /// <value>
        ///     The send time out value of the connection in seconds.
        /// </value>
        public int SendTimeout
        {
            get { return _listener?.SendTimeout ?? 0; }
            set
            {
                if (_listener != null)
                {
                    _listener.SendTimeout = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the value of the connection's linger option.
        /// </summary>
        /// <exception cref="NullReferenceException"> Thrown when a value was unexpectedly null. </exception>
        /// <value>
        ///     The linger option.
        /// </value>
        public LingerOption LingerState
        {
            get { return _listener?.LingerState ?? throw new NullReferenceException(nameof(LingerState)); }
            set
            {
                if (_listener != null)
                {
                    _listener.LingerState = value;
                }
            }
        }

        /// <summary>
        ///     Enables or disables delay when send or receive buffers are full.
        /// </summary>
        /// <exception cref="NullReferenceException"> Thrown when a value was unexpectedly null. </exception>
        /// <value>
        ///     The no delay state.
        /// </value>
        public bool NoDelay
        {
            get { return _listener?.NoDelay ?? throw new NullReferenceException(nameof(LingerState)); }
            set
            {
                if (_listener != null)
                {
                    _listener.NoDelay = value;
                }
            }
        }

        private protected abstract ushort MaxPayloadSize { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerBase{T, TServerClient}" /> class.
        /// </summary>
        /// <param name="listenerCount"> (Optional) The listener count. </param>
        private protected ServerBase(byte listenerCount = 1)
        {
            _listenerCount         = listenerCount;
            _dataReceivedCallbacks = new Dictionary<ushort, ServerClientEventEntry<TServerClient>>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<ushort, TaskCompletionSource<(ushort requestID, Packet packet)>>(
                    INITIAL_TASK_COMPLETION_QUEUE_SIZE);
            _clients                   = new Dictionary<T, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);
            _clientGuids               = new Dictionary<Guid, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);
            _clientsLock               = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
            _lockTaskCompletionSources = new SpinLock(Debugger.IsAttached);

            _requestID = 1;
            _packetID  = 1;

            _clientDataReceived = new Event<ClientCommandDataReceivedHandler<TServerClient>>();
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="ServerBase{T, TServerClient}" /> class.
        /// </summary>
        ~ServerBase()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Runs.
        /// </summary>
        /// <param name="port">               The port. </param>
        /// <param name="overwriteConfigure"> (Optional) Overwrite the default configuration. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        public bool Run(int port, Action<ServerBase<T, TServerClient>>? overwriteConfigure = null)
        {
            if (_isRunning) { return true; }
#pragma warning disable IDE0067 // Dispose objects before losing scope
            if (OnRun(port, out _listener))
#pragma warning restore IDE0067 // Dispose objects before losing scope
            {
                Configure();
                overwriteConfigure?.Invoke(this);

                _port  = port;
                _state = RECEIVE_FLAG | SEND_FLAG;
                for (int i = 0; i < _listenerCount; i++)
                {
                    ListenAsync();
                }
                return _isRunning = true;
            }
            return false;
        }

        /// <summary>
        ///     Called after the <see cref="Run" /> method directly after the socket is successfully created.
        /// </summary>
        private protected abstract void Configure();

        /// <summary>
        ///     Executes the run action.
        /// </summary>
        /// <param name="port">     The port. </param>
        /// <param name="listener"> [out] The listener. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
#if NETSTANDARD2_1
        private protected abstract bool OnRun(int port, [NotNullWhen(true)] out Socket? listener);
#else
        private protected abstract bool OnRun(int port, out Socket? listener);
#endif
        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
        private protected abstract void ListenAsync();

        /// <summary>
        ///     Deserialize data.
        /// </summary>
        /// <param name="arg0">                  Socket|Endpoint. </param>
        /// <param name="deserializePacketInfo"> Information describing the deserialize packet. </param>
        private protected void DeserializeData(T                        arg0,
                                               in DeserializePacketInfo deserializePacketInfo)
        {
            ushort commandOrResponseID = deserializePacketInfo.CommandOrResponseID;
            ushort requestID           = deserializePacketInfo.RequestID;

            if (deserializePacketInfo.IsResponseBitSet)
            {
                TaskCompletionSource<(ushort, Packet)>? cs;
                bool                                    lockTaken = false;
                try
                {
                    _lockTaskCompletionSources.Enter(ref lockTaken);
                    if (_taskCompletionSources.TryGetValue(commandOrResponseID, out cs))
                    {
                        _taskCompletionSources.Remove(commandOrResponseID);
                    }
                }
                finally
                {
                    if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
                }
                if (cs != null && !cs.TrySetResult(
                    (requestID,
                     new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length))))
                {
                    ByteArrayPool.Return(deserializePacketInfo.Data);
                }
                return;
            }
            switch (commandOrResponseID)
            {
                case CommandID.PING:
                    {
                        SendTo(
                            arg0, CommandID.PING,
                            deserializePacketInfo.Data, 0, deserializePacketInfo.Length,
                            requestID);
                        break;
                    }
                case CommandID.CONNECT:
                    {
                        deserializePacketInfo.Data.FromBytesUnsafe(out ConnectPacket connectPacket);
                        connectPacket.Rejected = !InvokeClientConnected(arg0);
                        connectPacket.ToBytesUnsafe2(out byte[] response, out int length);
                        SendTo(arg0, CommandID.CONNECT, response, 0, length, requestID);
                        break;
                    }
                case CommandID.DISCONNECT:
                    {
                        if (_clients.TryGetValue(arg0, out TServerClient sClient))
                        {
                            deserializePacketInfo.Data.FromBytesUnsafe(out DisconnectPacket disconnectPacket);
                            InvokeClientDisconnect(sClient, disconnectPacket.Reason);
                        }
                        break;
                    }
                default:
                    {
                        if (_clients.TryGetValue(arg0, out TServerClient sClient))
                        {
                            if (commandOrResponseID <= Constants.USER_COMMAND_LIMIT &&
                                _dataReceivedCallbacks.TryGetValue(
                                    commandOrResponseID, out ServerClientEventEntry<TServerClient>? scee))
                            {
                                sClient.SetLastReceivedPacketTimeStamp();

                                Packet packet = new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length);
                                ThreadPool.QueueUserWorkItem(
                                    x =>
                                    {
                                        object? res = scee._deserialize(in packet);
                                        ByteArrayPool.Return(packet.Buffer);

                                        if (res != null)
                                        {
                                            for (int i = _clientDataReceived.Count - 1; i >= 0; --i)
                                            {
                                                if (!_clientDataReceived[i]
                                                    .Invoke(this, sClient, commandOrResponseID, res, requestID))
                                                {
                                                    _clientDataReceived.Remove(i);
                                                }
                                            }
                                            scee.Raise(this, sClient, res, requestID);
                                        }
                                    });
                                return;
                            }
                        }
                        break;
                    }
            }
            ByteArrayPool.Return(deserializePacketInfo.Data);
        }

        /// <summary>
        ///     Called than a new client is connected.
        /// </summary>
        /// <param name="client"> The client. </param>
        protected virtual void OnClientConnected(TServerClient client) { }

        /// <summary>
        ///     Create a new ServerClient than a client connects.
        /// </summary>
        /// <param name="serverClient"> [out] out new ServerClient. </param>
        /// <returns>
        ///     <c>true</c> if the new ServerClient should be added to the clients list; <c>false</c> otherwise.
        /// </returns>
        protected abstract bool CreateServerClient(out TServerClient serverClient);

        /// <summary>
        ///     Executes the client disconnect on a different thread, and waits for the result.
        /// </summary>
        /// <param name="arg0">   Socket|Endpoint. </param>
        /// <param name="reason"> DisconnectReason. </param>
        private protected void InvokeClientDisconnect(T? arg0, DisconnectReason reason)
        {
            if (arg0 != null && _clients.TryGetValue(arg0, out TServerClient client))
            {
                InvokeClientDisconnect(client, reason);
            }
        }

        /// <summary>
        ///     Executes the client disconnect on a different thread, and waits for the result.
        /// </summary>
        /// <param name="client"> The client. </param>
        /// <param name="reason"> DisconnectReason. </param>
        private protected void InvokeClientDisconnect(TServerClient client, DisconnectReason reason)
        {
            bool lockTaken = false;
            bool removed;
            try
            {
                _clientsLock.Enter(ref lockTaken);
                _clientGuids.Remove(client.Guid);
                removed = _clients.Remove(client.Arg0);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }

            Task.Run(
                () =>
                {
                    if (removed)
                    {
                        OnClientDisconnected(client, reason);
                        ClientDisconnected?.Invoke(this, client, reason);
                    }

                    OnAfterClientDisconnect(client);
                });
        }

        /// <summary>
        ///     Called then the client is disconnected.
        /// </summary>
        /// <param name="client"> The client. </param>
        /// <param name="reason"> DisconnectReason. </param>
        protected virtual void OnClientDisconnected(TServerClient client, DisconnectReason reason) { }

        /// <summary>
        ///     called after <see cref="InvokeClientDisconnect(TServerClient, DisconnectReason)" />.
        /// </summary>
        /// <param name="client"> The client. </param>
        private protected virtual void OnAfterClientDisconnect(TServerClient client) { }

        private bool InvokeClientConnected(T arg0)
        {
            if (!CreateServerClient(out TServerClient serverClient)) { return false; }

            serverClient.Arg0 = arg0;
            bool lockTaken = false;
            try
            {
                _clientsLock.Enter(ref lockTaken);
                _clients.Add(arg0, serverClient);
                _clientGuids.Add(serverClient.Guid, serverClient);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }
            Task.Run(
                () =>
                {
                    OnClientConnected(serverClient);
                    ClientConnected?.Invoke(this, serverClient);
                });

            return true;
        }

        /// <inheritdoc />
        public void Disconnect(TServerClient client, DisconnectReason reason)
        {
            InvokeClientDisconnect(client, reason);
            SendTo(client, CommandID.DISCONNECT, new DisconnectPacket(reason));
        }

        /// <inheritdoc />
        public bool TryGetClient(Guid guid, out TServerClient client)
        {
            return _clientGuids.TryGetValue(guid, out client);
        }

        #region Add & Remove

        /// <summary>
        ///     add a command deserializer.
        /// </summary>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="deserialize"> The deserialize handler. </param>
        public void AddCommand(ushort commandID, DeserializePacketHandler<object?> deserialize)
        {
            AddCommand(new[] { commandID }, deserialize);
        }

        /// <summary>
        ///     add commands deserializers.
        /// </summary>
        /// <param name="commandIDs"> The command ids. </param>
        /// <param name="deserialize"> The deserialize handler. </param>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        public void AddCommand(ushort[] commandIDs, DeserializePacketHandler<object?> deserialize)
        {
            if (commandIDs.Length <= 0) { throw new ArgumentNullException(nameof(commandIDs)); }
            if (deserialize == null) { throw new ArgumentNullException(nameof(deserialize)); }

            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);

                foreach (ushort commandID in commandIDs)
                {
                    if (commandID > Constants.USER_COMMAND_LIMIT)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
                    }
                    if (!_dataReceivedCallbacks.TryGetValue(
                        commandID, out ServerClientEventEntry<TServerClient>? buffer))
                    {
                        buffer = new ServerClientEventEntry<TServerClient>(deserialize);
                        _dataReceivedCallbacks.Add(commandID, buffer);
                    }
                }
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }
        }

        /// <summary>
        ///     Removes the commands described by commandIDs.
        /// </summary>
        /// <param name="commandIDs"> A variable-length parameters list containing command ids. </param>
        /// <returns>
        ///     True if at least one command is removed, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        public bool RemoveCommands(params ushort[] commandIDs)
        {
            if (commandIDs.Length <= 0) { throw new ArgumentNullException(nameof(commandIDs)); }
            bool removed   = false;
            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                foreach (ushort commandID in commandIDs)
                {
                    if (commandID > Constants.USER_COMMAND_LIMIT)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
                    }
                    removed |= _dataReceivedCallbacks.Remove(commandID);
                }
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }
            return removed;
        }

        /// <summary>
        ///     add a data received callback.
        /// </summary>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="callback">  ClientDataReceivedHandler{Socket|Endpoint} </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="Exception">                   Thrown when an exception error condition occurs. </exception>
        public void AddDataReceivedCallback(ushort commandID, ClientDataReceivedHandler<TServerClient> callback)
        {
            if (commandID > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                if (!_dataReceivedCallbacks.TryGetValue(commandID, out ServerClientEventEntry<TServerClient>? buffer))
                {
                    throw new Exception(
                        $"Invalid parameter '{nameof(commandID)}'! Use 'AddCommand(DeserializeData, params uint[])' first.");
                }

                buffer.Add(callback);
            }
            finally
            {
                if (lockTaken) { _dataReceivedCallbacksLock.Exit(false); }
            }
        }

        /// <summary>
        ///     remove a data received callback.
        /// </summary>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="callback">  ClientDataReceivedHandler{Socket|Endpoint} </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        public void RemoveDataReceivedCallback(ushort commandID, ClientDataReceivedHandler<TServerClient> callback)
        {
            if (commandID > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            if (_dataReceivedCallbacks.TryGetValue(commandID, out ServerClientEventEntry<TServerClient>? buffer))
            {
                buffer.Remove(callback);
            }
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
                        _listener?.Dispose();
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