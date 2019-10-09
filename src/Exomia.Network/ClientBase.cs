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
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Exceptions;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

namespace Exomia.Network
{
    /// <summary>
    ///     A TCP/UDP-Client base.
    /// </summary>
    public abstract class ClientBase : IClient
    {
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
        ///     Initial size of the task completion queue.
        /// </summary>
        private const int INITIAL_TASK_COMPLETION_QUEUE_SIZE = 128;

        /// <summary>
        ///     The close timeout.
        /// </summary>
        private const int CLOSE_TIMEOUT = 10;

        /// <summary>
        ///     The default timeout.
        /// </summary>
        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     called than the client is Disconnected.
        /// </summary>
        public event DisconnectedHandler? Disconnected;

        /// <summary>
        ///     Occurs when data from a client is received.
        /// </summary>
        public event CommandDataReceivedHandler DataReceived
        {
            add { _dataReceived.Add(value); }
            remove { _dataReceived.Remove(value); }
        }

        /// <summary>
        ///     called than a ping is received.
        /// </summary>
        public event Action<PingPacket>? Ping;

        /// <summary>
        ///     The big data handler.
        /// </summary>
        private protected readonly BigDataHandler _bigDataHandler;

        /// <summary>
        ///     The client socket.
        /// </summary>
        private protected Socket? _clientSocket;

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
        ///     The connect checksum.
        /// </summary>
        private readonly byte[] _connectChecksum = new byte[16];

        /// <summary>
        ///     The manuel reset event.
        /// </summary>
        private readonly ManualResetEvent _manuelResetEvent;

        /// <summary>
        ///     The data received callbacks.
        /// </summary>
        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        /// <summary>
        ///     The task completion sources.
        /// </summary>
        private readonly Dictionary<uint, TaskCompletionSource<Packet>> _taskCompletionSources;

        /// <summary>
        ///     The listener count.
        /// </summary>
        private readonly byte _listenerCount;

        /// <summary>
        ///     The client data received event handler.
        /// </summary>
        private readonly Event<CommandDataReceivedHandler> _dataReceived;

        /// <summary>
        ///     The data received callbacks lock.
        /// </summary>
        private SpinLock _dataReceivedCallbacksLock;

        /// <summary>
        ///     The lock task completion sources.
        /// </summary>
        private SpinLock _lockTaskCompletionSources;

        /// <summary>
        ///     The port.
        /// </summary>
        private int _port;

        /// <summary>
        ///     Identifier for the response.
        /// </summary>
        private uint _responseID;

        /// <summary>
        ///     The server address.
        /// </summary>
        private string _serverAddress;

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
        ///     Gets the server address.
        /// </summary>
        /// <value>
        ///     The server address.
        /// </value>
        public string ServerAddress
        {
            get
            {
                if (_state != 0) { return _serverAddress; }
                throw new NotConnectedException("You have to connect to a server first!");
            }
        }

        /// <summary>
        ///     Gets the maximum size of the payload.
        /// </summary>
        /// <value>
        ///     The size of the maximum payload.
        /// </value>
        private protected abstract ushort MaxPayloadSize { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientBase" /> class.
        /// </summary>
        /// <param name="listenerCount"> (Optional) The listener count. </param>
        private protected ClientBase(byte listenerCount = 1)
        {
            _listenerCount         = listenerCount;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<uint, TaskCompletionSource<Packet>>(INITIAL_TASK_COMPLETION_QUEUE_SIZE);

            _lockTaskCompletionSources = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);

            _dataReceived = new Event<CommandDataReceivedHandler>();

            _responseID = 1;

            _bigDataHandler = new BigDataHandler();

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);
            _serverAddress    = default!;
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="ClientBase" /> class.
        /// </summary>
        ~ClientBase()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public bool Connect(IPAddress[] ipAddresses, int port, int timeout = 10)
        {
            Disconnect(DisconnectReason.Graceful);

            _manuelResetEvent.Reset();

#pragma warning disable IDE0067 // Dispose objects before losing scope
            if (TryCreateSocket(out _clientSocket))
#pragma warning restore IDE0067 // Dispose objects before losing scope
            {
                try
                {
                    IAsyncResult iar    = _clientSocket!.BeginConnect(ipAddresses, port, null, null);
                    bool         result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                    _clientSocket.EndConnect(iar);

                    _serverAddress = _clientSocket.RemoteEndPoint.ToString() ?? "<invalid>";

                    if (result)
                    {
                        _state = RECEIVE_FLAG | SEND_FLAG;
                        for (int i = 0; i < _listenerCount; i++)
                        {
                            ReceiveAsync();
                        }
                        if (SendConnect() == SendError.None)
                        {
                            _port = port;
                            return _manuelResetEvent.WaitOne(timeout * 1000);
                        }
                    }
                }
                catch
                {
                    _state = 0;
                    _clientSocket?.Close();
                    _clientSocket?.Dispose();
                    _clientSocket = null;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool Connect(string serverAddress, int port, int timeout = 10)
        {
            return Connect(Dns.GetHostAddresses(serverAddress), port, timeout);
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            Disconnect(DisconnectReason.Graceful);
        }

        /// <summary>
        ///     Sequence equal.
        /// </summary>
        /// <param name="left">   [in,out] If non-null, the left. </param>
        /// <param name="right">  [in,out] If non-null, the right. </param>
        /// <param name="length"> The length. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private static unsafe bool SequenceEqual(byte* left, byte* right, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (*(left + i) != *(right + i))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        ///     Attempts to create socket.
        /// </summary>
        /// <param name="socket"> [out] The socket. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private protected abstract bool TryCreateSocket(out Socket? socket);

        /// <summary>
        ///     Disconnects the given reason.
        /// </summary>
        /// <param name="reason"> The reason to disconnect. </param>
        private protected void Disconnect(DisconnectReason reason)
        {
            if (_clientSocket != null && _state != 0)
            {
                if (reason != DisconnectReason.Aborted && reason != DisconnectReason.Error)
                {
                    Send(CommandID.DISCONNECT, new byte[] { 255 }, 0, 1);
                }
                _state = 0;
                try
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close(CLOSE_TIMEOUT);
                    _clientSocket.Dispose();
                }
                catch
                {
                    /* IGNORE */
                }
                _clientSocket = null;
                Disconnected?.Invoke(this, reason);
            }
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        private protected abstract void ReceiveAsync();

        /// <summary>
        ///     Deserialize data.
        /// </summary>
        /// <param name="deserializePacketInfo"> Information describing the deserialize packet. </param>
        private protected unsafe void DeserializeData(in DeserializePacketInfo deserializePacketInfo)
        {
            uint commandID  = deserializePacketInfo.CommandID;
            uint responseID = deserializePacketInfo.ResponseID;
            if (responseID != 0)
            {
                TaskCompletionSource<Packet>? cs;
                bool                          lockTaken = false;
                try
                {
                    _lockTaskCompletionSources.Enter(ref lockTaken);
                    if (_taskCompletionSources.TryGetValue(responseID, out cs))
                    {
                        _taskCompletionSources.Remove(responseID);
                    }
                }
                finally
                {
                    if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
                }
                if (cs != null && !cs.TrySetResult(
                        new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length)))
                {
                    ByteArrayPool.Return(deserializePacketInfo.Data);
                }
                return;
            }
            switch (commandID)
            {
                case CommandID.PING:
                    {
                        PingPacket pingStruct;
                        fixed (byte* ptr = deserializePacketInfo.Data)
                        {
                            pingStruct = *(PingPacket*)ptr;
                        }
                        Ping?.Invoke(pingStruct);
                        break;
                    }
                case CommandID.CONNECT:
                    {
                        deserializePacketInfo.Data.FromBytesUnsafe(out ConnectPacket connectPacket);
                        fixed (byte* ptr = _connectChecksum)
                        {
                            if (SequenceEqual(connectPacket.Checksum, ptr, 16))
                            {
                                _manuelResetEvent.Set();
                            }
                        }
                        break;
                    }
                default:
                    {
                        if (commandID <= Constants.USER_COMMAND_LIMIT &&
                            _dataReceivedCallbacks.TryGetValue(commandID, out ClientEventEntry? cee))
                        {
                            Packet packet = new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length);
                            ThreadPool.QueueUserWorkItem(
                                x =>
                                {
                                    object res = cee._deserialize(in packet);
                                    ByteArrayPool.Return(packet.Buffer);

                                    if (res != null)
                                    {
                                        for (int i = _dataReceived.Count - 1; i >= 0; --i)
                                        {
                                            if (!_dataReceived[i].Invoke(this, commandID, res))
                                            {
                                                _dataReceived.Remove(i);
                                            }
                                        }
                                        cee.Raise(this, res);
                                    }
                                });
                            return;
                        }
                        break;
                    }
            }
            ByteArrayPool.Return(deserializePacketInfo.Data);
        }

        #region Add & Remove

        /// <summary>
        ///     add commands deserializers.
        /// </summary>
        /// <param name="deserialize"> The deserialize handler. </param>
        /// <param name="commandIDs">  A variable-length parameters list containing command ids. </param>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        public void AddCommand(DeserializePacketHandler<object> deserialize, params uint[] commandIDs)
        {
            if (commandIDs.Length <= 0) { throw new ArgumentNullException(nameof(commandIDs)); }

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
                        commandID, out ClientEventEntry? buffer))
                    {
                        buffer = new ClientEventEntry(deserialize);
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
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        public bool RemoveCommands(params uint[] commandIDs)
        {
            if (commandIDs.Length <= 0) { throw new ArgumentNullException(nameof(commandIDs)); }
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
        /// <param name="callback">  The callback. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="Exception">                   Thrown when an exception error condition occurs. </exception>
        public void AddDataReceivedCallback(uint commandID, DataReceivedHandler callback)
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
                if (!_dataReceivedCallbacks.TryGetValue(commandID, out ClientEventEntry? buffer))
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
        /// <param name="callback">  The callback. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        public void RemoveDataReceivedCallback(uint commandID, DataReceivedHandler callback)
        {
            if (commandID > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandID)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }

            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }

            if (_dataReceivedCallbacks.TryGetValue(commandID, out ClientEventEntry? buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        #region Send

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="packetInfo"> Information describing the packet. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private protected abstract SendError BeginSendData(in PacketInfo packetInfo);

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="commandID">  The command id. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseID"> The responseID. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        private unsafe SendError BeginSendData(uint   commandID,
                                               byte[] data,
                                               int    offset,
                                               int    length,
                                               uint   responseID)
        {
            if (_clientSocket == null) { return SendError.Invalid; }
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
                    byte[] buffer = new byte[LZ4Codec.MaximumOutputSize(length)];
                    int s = _compressionMode switch
                    {
                        CompressionMode.Lz4 => LZ4Codec.Encode(data, offset, length, buffer, 0, buffer.Length),
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(_compressionMode), _compressionMode, "Not supported!"),
                    };
                    if (s > 0 && s < length)
                    {
                        packetInfo.CompressedLength = s;
                        packetInfo.CompressionMode  = _compressionMode;
                        data                        = buffer;
                        offset                      = 0;
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
                        packetInfo.IsChunked   = false;
                        return BeginSendData(in packetInfo);
                    }
                    packetInfo.PacketID    = Interlocked.Increment(ref _packetID);
                    packetInfo.ChunkOffset = 0;
                    packetInfo.IsChunked   = true;
                    int chunkLength = packetInfo.CompressedLength;
                    while (chunkLength > MaxPayloadSize)
                    {
                        packetInfo.ChunkLength = MaxPayloadSize;
                        SendError se = BeginSendData(in packetInfo);
                        if (se != SendError.None)
                        {
                            return se;
                        }
                        chunkLength            -= MaxPayloadSize;
                        packetInfo.ChunkOffset += MaxPayloadSize;
                    }
                    packetInfo.ChunkLength = chunkLength;
                    return BeginSendData(in packetInfo);
                }
            }
            return SendError.Invalid;
        }

        /// <inheritdoc />
        public SendError Send(uint commandID, byte[] data, int offset, int length)
        {
            return BeginSendData(commandID, data, offset, length, 0);
        }

        /// <inheritdoc />
        public SendError Send(uint commandID, byte[] data)
        {
            return BeginSendData(commandID, data, 0, data.Length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data)
            where TResult : unmanaged
        {
            return SendR(commandID, data, 0, data.Length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, int offset, int length)
            where TResult : unmanaged
        {
            return SendR(commandID, data, offset, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      int                               offset,
                                                      int                               length,
                                                      DeserializePacketHandler<TResult> deserialize)
        {
            return SendR(commandID, data, offset, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize)
        {
            return SendR(commandID, data, 0, data.Length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint     commandID,
                                                      byte[]   data,
                                                      int      offset,
                                                      int      length,
                                                      TimeSpan timeout)
            where TResult : unmanaged
        {
            return SendR(commandID, data, offset, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint     commandID,
                                                      byte[]   data,
                                                      TimeSpan timeout)
            where TResult : unmanaged
        {
            return SendR(commandID, data, 0, data.Length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout)
        {
            return SendR(commandID, data, 0, data.Length, deserialize, timeout);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                            byte[]                            data,
                                                            int                               offset,
                                                            int                               length,
                                                            DeserializePacketHandler<TResult> deserialize,
                                                            TimeSpan                          timeout)
        {
            TaskCompletionSource<Packet> tcs =
                new TaskCompletionSource<Packet>(TaskCreationOptions.None);
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);
            uint                          responseID;
            bool                          lockTaken = false;
            try
            {
                _lockTaskCompletionSources.Enter(ref lockTaken);
                responseID = _responseID++;
                if (responseID == 0) { responseID++; }
                _taskCompletionSources.Add(responseID, tcs);
            }
            finally
            {
                if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
            }
            cts.Token.Register(
                delegate
                {
                    bool lockTaken1 = false;
                    try
                    {
                        _lockTaskCompletionSources.Enter(ref lockTaken1);
                        _taskCompletionSources.Remove(_responseID);
                    }
                    finally
                    {
                        if (lockTaken1) { _lockTaskCompletionSources.Exit(false); }
                    }
                    tcs.TrySetResult(default);
                }, false);
            SendError sendError = BeginSendData(commandID, data, offset, length, responseID);
            if (sendError == SendError.None)
            {
                Packet packet = await tcs.Task;
                if (packet.Buffer != null)
                {
                    TResult result = deserialize(in packet);
                    ByteArrayPool.Return(packet.Buffer);
                    return new Response<TResult>(result, SendError.None);
                }
                sendError = SendError.Unknown; //TimeOut Error
            }
            lockTaken = false;
            try
            {
                _lockTaskCompletionSources.Enter(ref lockTaken);
                _taskCompletionSources.Remove(_responseID);
            }
            finally
            {
                if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
            }
            return
                new Response<TResult>(
                    default!,
                    sendError); //can be null, but it doesn't matter if an error has occurred, it is unsafe to use it anyway.
        }

        /// <inheritdoc />
        public SendError Send(uint commandID, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return BeginSendData(commandID, dataB, 0, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable, TimeSpan timeout)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, deserialize, timeout);
        }

        /// <inheritdoc />
        public SendError Send<T>(uint commandID, in T data) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return BeginSendData(commandID, dataB, 0, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                         in T                              data,
                                                         DeserializePacketHandler<TResult> deserialize)
            where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data, TimeSpan timeout)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                         in T                              data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         TimeSpan                          timeout) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, deserialize, timeout);
        }

        /// <summary>
        ///     Deserialize response.
        /// </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <returns>
        ///     A TResult.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult DeserializeResponse<TResult>(in Packet packet)
            where TResult : unmanaged
        {
            return packet.Buffer.FromBytesUnsafe2<TResult>(packet.Offset);
        }

        /// <inheritdoc />
        public SendError SendPing()
        {
            return Send(CommandID.PING, new PingPacket { Timestamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public Task<Response<PingPacket>> SendRPing()
        {
            return SendR<PingPacket, PingPacket>(
                CommandID.PING, new PingPacket(DateTime.Now.Ticks));
        }

        /// <summary>
        ///     Sends the connect.
        /// </summary>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private unsafe SendError SendConnect()
        {
            ConnectPacket packet;
            fixed (byte* ptr = _connectChecksum)
            {
                Mem.Cpy(packet.Checksum, ptr, sizeof(byte) * 16);
            }
            return Send(CommandID.CONNECT, packet);
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
        ///     Releases the unmanaged resources used by the Exomia.Network.ClientBase and optionally
        ///     releases the managed resources.
        /// </summary>
        /// <param name="disposing"> disposing. </param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                OnDispose(disposing);
                if (disposing)
                {
                    Disconnect(DisconnectReason.Graceful);
                    _manuelResetEvent?.Dispose();
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