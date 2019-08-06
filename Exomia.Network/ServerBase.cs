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
using K4os.Compression.LZ4;

namespace Exomia.Network
{
    /// <summary>
    ///     A server base.
    /// </summary>
    /// <typeparam name="T">             Socket|Endpoint. </typeparam>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract class ServerBase<T, TServerClient> : IServer<TServerClient>
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
        ///     Called than a client is connected.
        /// </summary>
        public event ClientActionHandler<TServerClient> ClientConnected;

        /// <summary>
        ///     Called than a client is disconnected.
        /// </summary>
        public event ClientDisconnectHandler<TServerClient> ClientDisconnected;

        /// <summary>
        ///     Occurs when data from a client is received.
        /// </summary>
        public event ClientCommandDataReceivedHandler<TServerClient> ClientDataReceived
        {
            add { _clientDataReceived.Add(value); }
            remove { _clientDataReceived.Remove(value); }
        }

        /// <summary>
        ///     The clients.
        /// </summary>
        protected readonly Dictionary<T, TServerClient> _clients;

        /// <summary>
        ///     The big data handler.
        /// </summary>
        private protected readonly BigDataHandler _bigDataHandler;

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
        ///     The compression mode.
        /// </summary>
        protected CompressionMode _compressionMode = CompressionMode.Lz4;

        /// <summary>
        ///     The encryption mode.
        /// </summary>
        private protected EncryptionMode _encryptionMode = EncryptionMode.None;

        /// <summary>
        ///     The data received callbacks.
        /// </summary>
        private readonly Dictionary<uint, ServerClientEventEntry<TServerClient>> _dataReceivedCallbacks;

        /// <summary>
        ///     The client data received event handler.
        /// </summary>
        private readonly Event<ClientCommandDataReceivedHandler<TServerClient>> _clientDataReceived;

        /// <summary>
        ///     The listener count.
        /// </summary>
        private readonly byte _listenerCount;

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
        ///     Identifier for the packet.
        /// </summary>
        private int _packetID;

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
        ///     Gets the maximum size of the payload.
        /// </summary>
        private protected abstract ushort MaxPayloadSize { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerBase{T, TServerClient}" /> class.
        /// </summary>
        /// <param name="listenerCount"> (Optional) The listener count. </param>
        private protected ServerBase(byte listenerCount = 1)
        {
            _listenerCount         = listenerCount;
            _dataReceivedCallbacks = new Dictionary<uint, ServerClientEventEntry<TServerClient>>(INITIAL_QUEUE_SIZE);
            _clients               = new Dictionary<T, TServerClient>(INITIAL_CLIENT_QUEUE_SIZE);

            _clientsLock               = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);

            _packetID = 1;

            _clientDataReceived = new Event<ClientCommandDataReceivedHandler<TServerClient>>();

            _bigDataHandler = new BigDataHandler();
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
        /// <param name="port"> Port. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        public bool Run(int port)
        {
            if (_isRunning) { return true; }
            if (OnRun(port, out _listener))
            {
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
        /// <param name="arg0">                  Socket|Endpoint. </param>
        /// <param name="deserializePacketInfo"> Information describing the deserialize packet. </param>
        private protected void DeserializeData(T                        arg0,
                                               in DeserializePacketInfo deserializePacketInfo)
        {
            uint commandID  = deserializePacketInfo.CommandID;
            uint responseID = deserializePacketInfo.ResponseID;
            switch (commandID)
            {
                case CommandID.PING:
                    {
                        SendTo(
                            arg0, CommandID.PING,
                            deserializePacketInfo.Data, 0, deserializePacketInfo.Length,
                            responseID);
                        break;
                    }
                case CommandID.CONNECT:
                    {
                        InvokeClientConnected(arg0);
                        SendTo(
                            arg0, CommandID.CONNECT,
                            deserializePacketInfo.Data, 0, deserializePacketInfo.Length,
                            responseID);
                        break;
                    }
                case CommandID.DISCONNECT:
                    {
                        if (_clients.TryGetValue(arg0, out TServerClient sClient))
                        {
                            InvokeClientDisconnect(sClient, DisconnectReason.Graceful);
                        }
                        break;
                    }
                default:
                    {
                        if (_clients.TryGetValue(arg0, out TServerClient sClient))
                        {
                            if (commandID <= Constants.USER_COMMAND_LIMIT &&
                                _dataReceivedCallbacks.TryGetValue(
                                    commandID, out ServerClientEventEntry<TServerClient> scee))
                            {
                                sClient.SetLastReceivedPacketTimeStamp();

                                Packet packet = new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length);
                                ThreadPool.QueueUserWorkItem(
                                    x =>
                                    {
                                        object res = scee._deserialize(in packet);
                                        ByteArrayPool.Return(packet.Buffer);

                                        if (res != null)
                                        {
                                            for (int i = _clientDataReceived.Count - 1; i >= 0; --i)
                                            {
                                                if (!_clientDataReceived[i]
                                                    .Invoke(this, sClient, commandID, res, responseID))
                                                {
                                                    _clientDataReceived.Remove(i);
                                                }
                                            }
                                            scee.Raise(this, sClient, res, responseID);
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
        private protected void InvokeClientDisconnect(T arg0, DisconnectReason reason)
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
                removed = _clients.Remove(client.Arg0);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }

            if (removed)
            {
                OnClientDisconnected(client, reason);
                ClientDisconnected?.Invoke(this, client, reason);
            }

            OnAfterClientDisconnect(client);
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

        /// <summary>
        ///     Executes the client connected on a different thread, and waits for the result.
        /// </summary>
        /// <param name="arg0"> Socket|Endpoint. </param>
        private void InvokeClientConnected(T arg0)
        {
            if (CreateServerClient(out TServerClient serverClient))
            {
                serverClient.Arg0 = arg0;
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

                OnClientConnected(serverClient);
                ClientConnected?.Invoke(this, serverClient);
            }
        }

        #region Add & Remove

        /// <summary>
        ///     add commands deserializers.
        /// </summary>
        /// <param name="deserialize"> The deserialize handler. </param>
        /// <param name="commandIDs">  A variable-length parameters list containing command ids. </param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when one or more required arguments
        ///     are null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public void AddCommand(DeserializePacketHandler<object> deserialize, params uint[] commandIDs)
        {
            if (commandIDs == null) { throw new ArgumentNullException(nameof(commandIDs)); }
            if (deserialize == null) { throw new ArgumentNullException(nameof(deserialize)); }

            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);

                foreach (uint commandID in commandIDs)
                {
                    if (commandID > Constants.USER_COMMAND_LIMIT)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
                    }
                    if (!_dataReceivedCallbacks.TryGetValue(
                        commandID, out ServerClientEventEntry<TServerClient> buffer))
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
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public bool RemoveCommands(params uint[] commandIDs)
        {
            bool removed   = false;
            bool lockTaken = false;
            try
            {
                _dataReceivedCallbacksLock.Enter(ref lockTaken);
                foreach (uint commandID in commandIDs)
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
        public void AddDataReceivedCallback(uint commandID, ClientDataReceivedHandler<TServerClient> callback)
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
                if (!_dataReceivedCallbacks.TryGetValue(commandID, out ServerClientEventEntry<TServerClient> buffer))
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
        public void RemoveDataReceivedCallback(uint commandID, ClientDataReceivedHandler<TServerClient> callback)
        {
            if (commandID > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            if (_dataReceivedCallbacks.TryGetValue(commandID, out ServerClientEventEntry<TServerClient> buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        #region Send

        /// <summary>
        ///     Sends to.
        /// </summary>
        /// <param name="arg0">       Socket|Endpoint. </param>
        /// <param name="packetInfo"> Information describing the packet. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private protected abstract SendError SendTo(T             arg0,
                                                    in PacketInfo packetInfo);

        /// <summary>
        ///     Sends to.
        /// </summary>
        /// <param name="arg0">       Socket|Endpoint. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseID"> Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private unsafe SendError SendTo(T      arg0,
                                        uint   commandID,
                                        byte[] data,
                                        int    offset,
                                        int    length,
                                        uint   responseID)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                PacketInfo packetInfo;
                packetInfo.CommandID        = commandID;
                packetInfo.ResponseID       = responseID;
                packetInfo.Length           = length;
                packetInfo.CompressedLength = length;
                packetInfo.CompressionMode  = CompressionMode.None;
                if (length >= Constants.LENGTH_THRESHOLD && _compressionMode != CompressionMode.None)
                {
                    int    s;
                    byte[] buffer = new byte[LZ4Codec.MaximumOutputSize(length)];
                    switch (_compressionMode)
                    {
                        case CompressionMode.Lz4:
                            s = LZ4Codec.Encode(data, 0, length, buffer, 0, buffer.Length);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(_compressionMode), _compressionMode, "Not supported!");
                    }
                    if (s > 0 && s < length)
                    {
                        packetInfo.CompressedLength = s;
                        packetInfo.CompressionMode  = _compressionMode;
                        data                        = buffer;
                    }
                }

                fixed (byte* src = data)
                {
                    packetInfo.Src = src + offset;
                    if (packetInfo.CompressedLength <= MaxPayloadSize)
                    {
                        packetInfo.PacketID    = 0;
                        packetInfo.ChunkOffset = 0;
                        packetInfo.ChunkLength = packetInfo.CompressedLength;
                        packetInfo.Src         = src + offset;
                        packetInfo.IsChunked   = false;
                        return SendTo(arg0, in packetInfo);
                    }

                    packetInfo.PacketID    = Interlocked.Increment(ref _packetID);
                    packetInfo.ChunkOffset = 0;
                    packetInfo.IsChunked   = true;
                    int chunkLength = packetInfo.CompressedLength;
                    while (chunkLength > MaxPayloadSize)
                    {
                        packetInfo.ChunkLength = MaxPayloadSize;
                        SendError se = SendTo(arg0, in packetInfo);
                        if (se != SendError.None)
                        {
                            return se;
                        }
                        chunkLength            -= MaxPayloadSize;
                        packetInfo.ChunkOffset += MaxPayloadSize;
                    }
                    packetInfo.ChunkLength = chunkLength;
                    return SendTo(arg0, in packetInfo);
                }
            }
            return SendError.Invalid;
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                byte[]        data,
                                int           offset,
                                int           length,
                                uint          responseID)
        {
            return SendTo(client.Arg0, commandID, data, offset, length, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                byte[]        data,
                                uint          responseID)
        {
            return SendTo(client.Arg0, commandID, data, 0, data.Length, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                ISerializable serializable,
                                uint          responseID)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendTo(client.Arg0, commandID, dataB, 0, length, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo<T1>(TServerClient client,
                                    uint          commandID,
                                    in T1         data,
                                    uint          responseID)
            where T1 : unmanaged
        {
            byte[] dataB = data.ToBytesUnsafe2(out int length);
            return SendTo(client.Arg0, commandID, dataB, 0, length, responseID);
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, byte[] data, int offset, int length)
        {
            Dictionary<T, TServerClient> clients;
            bool                         lockTaken = false;
            try
            {
                _clientsLock.Enter(ref lockTaken);
                clients = new Dictionary<T, TServerClient>(_clients);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }

            if (clients.Count > 0)
            {
                foreach (T arg0 in clients.Keys)
                {
                    SendTo(arg0, commandID, data, offset, length, 0);
                }
            }
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, byte[] data)
        {
            SendToAll(commandID, data, 0, data.Length);
        }

        /// <inheritdoc />
        public void SendToAll<T1>(uint commandID, in T1 data)
            where T1 : unmanaged
        {
            byte[] buffer = data.ToBytesUnsafe2(out int length);
            SendToAll(commandID, buffer, 0, length);
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, ISerializable serializable)
        {
            byte[] buffer = serializable.Serialize(out int length);
            SendToAll(commandID, buffer, 0, length);
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