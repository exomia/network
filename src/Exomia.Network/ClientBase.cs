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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Exceptions;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
#if NETSTANDARD2_1
using System.Diagnostics.CodeAnalysis;

#endif

namespace Exomia.Network
{
    /// <summary>
    ///     A TCP/UDP-Client base.
    /// </summary>
    public abstract partial class ClientBase : IClient
    {
        private protected const byte     RECEIVE_FLAG                       = 0b0000_0001;
        private protected const byte     SEND_FLAG                          = 0b0000_0010;
        private const           int      INITIAL_QUEUE_SIZE                 = 16;
        private const           int      INITIAL_TASK_COMPLETION_QUEUE_SIZE = 128;
        private const           int      CLOSE_TIMEOUT                      = 10;
        private static readonly TimeSpan s_defaultTimeout                   = TimeSpan.FromSeconds(10);

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

        private protected Socket? _clientSocket;
        private protected byte    _state;

        /// <summary>
        ///     The compression mode.
        /// </summary>
        protected CompressionMode _compressionMode = CompressionMode.Lz4;

        private protected EncryptionMode _encryptionMode  = EncryptionMode.None;
        private readonly  byte[]         _connectChecksum = new byte[16];

        private readonly ManualResetEvent                     _manuelResetEvent;
        private readonly Dictionary<ushort, ClientEventEntry> _dataReceivedCallbacks;

        private readonly Dictionary<ushort, TaskCompletionSource<(ushort requestID, Packet packet)>>
            _taskCompletionSources;

        private readonly byte                              _listenerCount;
        private readonly Event<CommandDataReceivedHandler> _dataReceived;
        private          SpinLock                          _dataReceivedCallbacksLock;
        private          SpinLock                          _lockTaskCompletionSources;
        private          int                               _port;
        private          string                            _serverAddress;
        private          ushort                            _requestID;
        private          int                               _packetID;

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
        ///     Gets or sets the size of the receive buffer in bytes.
        /// </summary>
        /// <value>
        ///     The size of the receive buffer in bytes.
        /// </value>
        public int ReceiveBufferSize
        {
            get { return _clientSocket?.ReceiveBufferSize ?? 0; }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.ReceiveBufferSize = value;
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
            get { return _clientSocket?.SendBufferSize ?? 0; }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.ReceiveBufferSize = value;
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
            get { return _clientSocket?.ReceiveTimeout ?? 0; }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.ReceiveTimeout = value;
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
            get { return _clientSocket?.SendTimeout ?? 0; }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.SendTimeout = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the value of the connection's linger option.
        /// </summary>
        /// <value>
        ///     The linger option.
        /// </value>
        public LingerOption LingerState
        {
            get { return _clientSocket?.LingerState ?? throw new NullReferenceException(nameof(LingerState)); }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.LingerState = value;
                }
            }
        }

        /// <summary>
        ///     Enables or disables delay when send or receive buffers are full.
        /// </summary>
        /// <value>
        ///     The no delay state.
        /// </value>
        public bool NoDelay
        {
            get { return _clientSocket?.NoDelay ?? throw new NullReferenceException(nameof(LingerState)); }
            set
            {
                if (_clientSocket != null)
                {
                    _clientSocket.NoDelay = value;
                }
            }
        }

        private protected abstract ushort MaxPayloadSize { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientBase" /> class.
        /// </summary>
        /// <param name="listenerCount"> (Optional) The listener count. </param>
        private protected ClientBase(byte listenerCount = 1)
        {
            _listenerCount         = listenerCount;
            _dataReceivedCallbacks = new Dictionary<ushort, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<ushort, TaskCompletionSource<(ushort, Packet)>>(INITIAL_TASK_COMPLETION_QUEUE_SIZE);

            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
            _lockTaskCompletionSources = new SpinLock(Debugger.IsAttached);

            _dataReceived = new Event<CommandDataReceivedHandler>();

            _requestID = 1;
            _packetID  = 1;

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
        public bool Connect(IPAddress[]         ipAddresses,
                            int                 port,
                            Action<ClientBase>? overwriteConfigure = null,
                            int                 timeout            = 10)
        {
            Disconnect(DisconnectReason.Graceful);

            _manuelResetEvent.Reset();

#pragma warning disable IDE0067 // Dispose objects before losing scope
            if (TryCreateSocket(out _clientSocket))
#pragma warning restore IDE0067 // Dispose objects before losing scope
            {
                Configure();
                overwriteConfigure?.Invoke(this);

                try
                {
                    IAsyncResult iar    = _clientSocket!.BeginConnect(ipAddresses, port, null, null);
                    bool         result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                    _clientSocket.EndConnect(iar);

                    _serverAddress = _clientSocket?.RemoteEndPoint.ToString() ?? "<invalid>";

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

        /// <summary>
        ///     Called after the <see cref="Connect(System.Net.IPAddress[],int,System.Action{Exomia.Network.ClientBase},int)" />
        ///     method directly after the socket is successfully created.
        /// </summary>
        private protected abstract void Configure();

        /// <inheritdoc />
        public bool Connect(string              serverAddress,
                            int                 port,
                            Action<ClientBase>? overwriteConfigure = null,
                            int                 timeout            = 10)
        {
            return Connect(Dns.GetHostAddresses(serverAddress), port, overwriteConfigure, timeout);
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            Disconnect(DisconnectReason.Graceful);
        }

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

#if NETSTANDARD2_1
        private protected abstract bool TryCreateSocket([NotNullWhen(true)] out Socket? socket);
#else
        private protected abstract bool TryCreateSocket(out Socket? socket);
#endif

        private protected void Disconnect(DisconnectReason reason, bool noSend = true)
        {
            if (_clientSocket != null && _state != 0)
            {
                if (!noSend && reason != DisconnectReason.Aborted && reason != DisconnectReason.Error)
                {
                    Send(CommandID.DISCONNECT, new DisconnectPacket(reason));
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

        private protected abstract void ReceiveAsync();

        private protected unsafe void DeserializeData(in DeserializePacketInfo deserializePacketInfo)
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
                    (requestID, new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length))))
                {
                    ByteArrayPool.Return(deserializePacketInfo.Data);
                }
                return;
            }
            switch (commandOrResponseID)
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
                        if (!connectPacket.Rejected)
                        {
                            fixed (byte* ptr = _connectChecksum)
                            {
                                if (SequenceEqual(connectPacket.Checksum, ptr, 16))
                                {
                                    _manuelResetEvent.Set();
                                }
                            }
                        }
                        break;
                    }
                case CommandID.DISCONNECT:
                    {
                        deserializePacketInfo.Data.FromBytesUnsafe(out DisconnectPacket disconnectPacket);
                        Disconnect(disconnectPacket.Reason);
                        break;
                    }
                default:
                    {
                        if (commandOrResponseID <= Constants.USER_COMMAND_LIMIT &&
                            _dataReceivedCallbacks.TryGetValue(commandOrResponseID, out ClientEventEntry? cee))
                        {
                            Packet packet = new Packet(deserializePacketInfo.Data, 0, deserializePacketInfo.Length);
                            ThreadPool.QueueUserWorkItem(
                                x =>
                                {
                                    object? res = cee._deserialize(in packet);
                                    ByteArrayPool.Return(packet.Buffer);

                                    if (res != null)
                                    {
                                        for (int i = _dataReceived.Count - 1; i >= 0; --i)
                                        {
                                            if (!_dataReceived[i].Invoke(this, commandOrResponseID, res, requestID))
                                            {
                                                _dataReceived.Remove(i);
                                            }
                                        }
                                        cee.Raise(this, res, requestID);
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
            if (commandIDs == null || commandIDs.Length <= 0) { throw new ArgumentNullException(nameof(commandIDs)); }

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
        /// <param name="callback">  The callback. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        /// <exception cref="ArgumentNullException">       Thrown when one or more required arguments are null. </exception>
        /// <exception cref="Exception">                   Thrown when an exception error condition occurs. </exception>
        public void AddDataReceivedCallback(ushort commandID, DataReceivedHandler callback)
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
        public void RemoveDataReceivedCallback(ushort commandID, DataReceivedHandler callback)
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