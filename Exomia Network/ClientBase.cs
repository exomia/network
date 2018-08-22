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
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Native;
using Exomia.Network.Buffers;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;
using Debugger = System.Diagnostics.Debugger;

namespace Exomia.Network
{
    /// <inheritdoc cref="IClient" />
    /// <summary>
    ///     A TCP/UDP-Client base
    /// </summary>
    public abstract class ClientBase : IClient
    {
        /// <summary>
        /// </summary>
        protected const byte RECEIVE_FLAG = 0b0000_0001;

        /// <summary>
        /// </summary>
        protected const byte SEND_FLAG = 0b0000_0010;

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_TASKCOMPLETION_QUEUE_SIZE = 128;

        private const int CLOSE_TIMEOUT = 10;

        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     called than the client is Disconnected
        /// </summary>
        public event DisconnectedHandler Disconnected;

        /// <summary>
        ///     called than a ping is received
        /// </summary>
        public event Action<PingPacket> Ping;

        /// <summary>
        /// </summary>
        protected Socket _clientSocket;

        /// <summary>
        /// </summary>
        protected byte _state;

        private readonly byte[] _connectChecksum = new byte[16];

        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        private readonly ManualResetEvent _manuelResetEvent;

        private readonly Dictionary<uint, TaskCompletionSource<Packet>> _taskCompletionSources;

        private SpinLock _dataReceivedCallbacksLock;
        private SpinLock _lockTaskCompletionSources;

        private int _port;

        private uint _responseID;
        private string _serverAddress;

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

        /// <summary>
        ///     ClientBase constructor
        /// </summary>
        protected ClientBase()
        {
            _clientSocket = null;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<uint, TaskCompletionSource<Packet>>(INITIAL_TASKCOMPLETION_QUEUE_SIZE);

            _lockTaskCompletionSources = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
            _responseID = 1;

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);
        }

        /// <summary>
        ///     ClientBase destructor
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
                    IAsyncResult iar = _clientSocket.BeginConnect(ipAddresses, port, null, null);
                    bool result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
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
        ///     tries to create a socket
        /// </summary>
        /// <param name="socket">the socket which was generated</param>
        /// <returns><c>true</c> if socket was successfully created; <c>false otherwise</c></returns>
        protected abstract bool TryCreateSocket(out Socket socket);

        /// <summary>
        /// </summary>
        /// <param name="reason"></param>
        protected void Disconnect(DisconnectReason reason)
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
        /// </summary>
        protected abstract void ReceiveAsync();

        private protected unsafe void DeserializeData(uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            if (responseid != 0)
            {
                TaskCompletionSource<Packet> cs;
                bool lockTaken = false;
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

                        /*Task.Factory.StartNew(() => {
                            object res = cee._deserialize(in packet);
                            ByteArrayPool.Return(data);
                            if (res != null) { cee.RaiseAsync(this, res); }
                        });*/

                        /*cee._deserialize.BeginInvoke(
                        in packet, iar =>
                        {
                            object res = cee._deserialize.EndInvoke(in packet, iar);
                            ByteArrayPool.Return(data);
                            if (res != null) { cee.RaiseAsync(this, res); }
                        }, null);*/
                        return;
                    }
                    break;
                }
            }
            ByteArrayPool.Return(data);
        }

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
        ///     remove a command
        /// </summary>
        /// <param name="commandid">command id</param>
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
        ///     add a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">callback</param>
        public void AddDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (commandid > Constants.USER_COMMAND_LIMIT)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(commandid)} is restricted to 0 - {Constants.USER_COMMAND_LIMIT}");
            }
            if (callback == null) { throw new ArgumentNullException(nameof(callback)); }
            ClientEventEntry buffer;
            bool lockTaken = false;
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
        ///     remove a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">DataReceivedHandler</param>
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
        /// </summary>
        /// <param name="commandid"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="responseID"></param>
        /// <returns></returns>
        protected abstract SendError BeginSendData(uint commandid, byte[] data, int offset, int length,
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
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length,
            DeserializePacket<TResult> deserialize)
        {
            return SendR(commandid, data, offset, length, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length,
            TimeSpan timeout)
            where TResult : unmanaged
        {
            return SendR(commandid, data, offset, length, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length,
            DeserializePacket<TResult> deserialize, TimeSpan timeout)
        {
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
                    if (packet.Buffer != null && deserialize != null)
                    {
                        TResult result = deserialize(in packet);
                        ByteArrayPool.Return(packet.Buffer);
                        return new Response<TResult>(result, SendError.None);
                    }
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
        public Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable,
            DeserializePacket<TResult> deserialize)
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
        public Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable,
            DeserializePacket<TResult> deserialize, TimeSpan timeout)
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
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data,
            DeserializePacket<TResult> deserialize) where T : unmanaged
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
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data,
            DeserializePacket<TResult> deserialize, TimeSpan timeout) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandid, dataB, 0, length, deserialize, timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult DeserializeResponse<TResult>(in Packet packet)
            where TResult : unmanaged
        {
            return packet.Buffer.FromBytesUnsafe2<TResult>(packet.Offset);
        }

        /// <inheritdoc />
        public SendError SendPing()
        {
            return Send(CommandID.PING, new PingPacket { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public Task<Response<PingPacket>> SendRPing()
        {
            return SendR<PingPacket, PingPacket>(
                CommandID.PING, new PingPacket(DateTime.Now.Ticks));
        }

        /// <inheritdoc />
        public unsafe SendError SendClientInfo(long clientID, string clientName)
        {
            ClientinfoPacket packet;
            packet.ClientID = clientID;
            fixed (char* ptr = clientName)
            {
                Mem.Cpy(packet.ClientName, ptr, sizeof(char) * Math.Max(0, Math.Min(clientName.Length, 64)));
            }
            return Send(CommandID.CLIENTINFO, packet);
        }

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
                    Disconnect(DisconnectReason.Graceful);
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