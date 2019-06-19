#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
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
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Native;
using Exomia.Network.Serialization;

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
        public event DisconnectedHandler Disconnected;

        /// <summary>
        ///     called than a ping is received.
        /// </summary>
        public event Action<PingPacket> Ping;

        /// <summary>
        ///     The client socket.
        /// </summary>
        private protected Socket _clientSocket;

        /// <summary>
        ///     The state.
        /// </summary>
        private protected byte _state;

        /// <summary>
        ///     The connect checksum.
        /// </summary>
        private readonly byte[] _connectChecksum = new byte[16];

        /// <summary>
        ///     The data received callbacks.
        /// </summary>
        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        /// <summary>
        ///     The manuel reset event.
        /// </summary>
        private readonly ManualResetEvent _manuelResetEvent;

        /// <summary>
        ///     The task completion sources.
        /// </summary>
        private readonly Dictionary<uint, TaskCompletionSource<Packet>> _taskCompletionSources;

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
        ///     ServerAddress.
        /// </summary>
        /// <value>
        ///     The server address.
        /// </value>
        public string ServerAddress
        {
            get { return _serverAddress; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientBase" /> class.
        /// </summary>
        private protected ClientBase()
        {
            _clientSocket          = null;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<uint, TaskCompletionSource<Packet>>(INITIAL_TASK_COMPLETION_QUEUE_SIZE);

            _lockTaskCompletionSources = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
            _responseID                = 1;

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);
        }

        /// <summary>
        ///     ClientBase destructor.
        /// </summary>
        ~ClientBase()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public bool Connect(IPAddress[] ipAddresses, int port, int timeout = 10)
        {
            Disconnect(DisconnectReason.Graceful);

            _port = port;

            _manuelResetEvent.Reset();

            if (TryCreateSocket(out _clientSocket))
            {
                try
                {
                    IAsyncResult iar    = _clientSocket.BeginConnect(ipAddresses, port, null, null);
                    bool         result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                    _clientSocket.EndConnect(iar);

                    _serverAddress = _clientSocket.RemoteEndPoint.ToString();

                    if (result)
                    {
                        _state = RECEIVE_FLAG | SEND_FLAG;
                        ReceiveAsync();
                        if (SendConnect() == SendError.None)
                        {
                            return _manuelResetEvent.WaitOne(timeout * 1000);
                        }
                    }
                }
                catch
                {
                    _state = 0;
                    _clientSocket.Close();
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
        private protected abstract bool TryCreateSocket(out Socket socket);

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
                    Send(CommandID.DISCONNECT, new byte[1] { 255 }, 0, 1);
                }
                _state = 0;
                try
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close(CLOSE_TIMEOUT);
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
        /// <param name="commandid">  command id. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseid"> The responseid. </param>
        private protected unsafe void DeserializeData(uint commandid, byte[] data, int offset, int length,
                                                      uint responseid)
        {
            if (responseid != 0)
            {
                TaskCompletionSource<Packet> cs;
                bool                         lockTaken = false;
                try
                {
                    _lockTaskCompletionSources.Enter(ref lockTaken);
                    if (_taskCompletionSources.TryGetValue(responseid, out cs))
                    {
                        _taskCompletionSources.Remove(responseid);
                    }
                }
                finally
                {
                    if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
                }
                if (cs != null && !cs.TrySetResult(
                        new Packet(data, offset, length)))
                {
                    ByteArrayPool.Return(data);
                }
                return;
            }
            switch (commandid)
            {
                case CommandID.PING:
                    {
                        PingPacket pingStruct;
                        fixed (byte* ptr = data)
                        {
                            pingStruct = *(PingPacket*)(ptr + offset);
                        }
                        Ping?.Invoke(pingStruct);
                        break;
                    }
                case CommandID.CONNECT:
                    {
                        data.FromBytesUnsafe(offset, out ConnectPacket connectPacket);
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
                        if (commandid <= Constants.USER_COMMAND_LIMIT &&
                            _dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry cee))
                        {
                            Packet packet = new Packet(data, offset, length);
                            ThreadPool.QueueUserWorkItem(
                                x =>
                                {
                                    object res = cee._deserialize(in packet);
                                    ByteArrayPool.Return(data);

                                    if (res != null) { cee.Raise(this, res); }
                                });
                            return;
                        }
                        break;
                    }
            }
            ByteArrayPool.Return(data);
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
                if (!_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
                {
                    buffer = new ClientEventEntry(deserialize);
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
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public void RemoveCommand(uint commandid)
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
                _dataReceivedCallbacks.Remove(commandid);
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
        /// <param name="callback">  callback. </param>
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
        public void AddDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }
            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }
            ClientEventEntry buffer;
            bool             lockTaken = false;
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
        /// <param name="callback">  DataReceivedHandler. </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when one or more required arguments
        ///     are null.
        /// </exception>
        public void RemoveDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }
            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        #region Send

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="commandid">  command id. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseID"> Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private protected abstract SendError BeginSendData(uint commandid, byte[] data, int offset, int length,
                                                           uint responseID);

        /// <inheritdoc />
        public SendError Send(uint commandid, byte[] data, int offset, int length)
        {
            return BeginSendData(commandid, data, offset, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length)
            where TResult : unmanaged
        {
            return SendR(commandid, data, offset, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandid, byte[] data,
                                                      int                               offset,    int    length,
                                                      DeserializePacketHandler<TResult> deserialize)
        {
            return SendR(commandid, data, offset, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint     commandid, byte[] data, int offset, int length,
                                                      TimeSpan timeout)
            where TResult : unmanaged
        {
            return SendR(commandid, data, offset, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(uint                              commandid, byte[] data,
                                                            int                               offset,    int    length,
                                                            DeserializePacketHandler<TResult> deserialize,
                                                            TimeSpan                          timeout)
        {
            if (deserialize == null) { throw new ArgumentNullException(nameof(deserialize)); }

            TaskCompletionSource<Packet> tcs =
                new TaskCompletionSource<Packet>(TaskCreationOptions.None);
            using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
            {
                uint responseID;
                bool lockTaken = false;
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
                SendError sendError = BeginSendData(commandid, data, offset, length, responseID);
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
                return new Response<TResult>(default, sendError);
            }
        }

        /// <inheritdoc />
        public SendError Send(uint commandid, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return BeginSendData(commandid, dataB, 0, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandid, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandid,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandid, dataB, 0, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable, TimeSpan timeout)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandid, dataB, 0, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandid,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandid, dataB, 0, length, deserialize, timeout);
        }

        /// <inheritdoc />
        public SendError Send<T>(uint commandid, in T data) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return BeginSendData(commandid, dataB, 0, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandid, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint                              commandid, in T data,
                                                         DeserializePacketHandler<TResult> deserialize)
            where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandid, dataB, 0, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data, TimeSpan timeout)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandid, dataB, 0, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint                              commandid, in T data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         TimeSpan                          timeout) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandid, dataB, 0, length, deserialize, timeout);
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