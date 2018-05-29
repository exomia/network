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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network
{
    /// <inheritdoc cref="IClient" />
    public sealed class Client : IClient, IDisposable
    {
        #region Variables

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_TASKCOMPLETION_QUEUE_SIZE = 128;

        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     called than the client is Disconnected
        /// </summary>
        public event DisconnectedHandler Disconnected;

        /// <summary>
        ///     called than a ping is received
        /// </summary>
        public event Action<PING_STRUCT> Ping;

        private readonly byte[] _connectChecksum = new byte[16];

        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        private readonly ManualResetEvent _manuelResetEvent;

        private readonly ClientStateObject _state;

        private readonly Dictionary<uint, TaskCompletionSource<Packet>> _taskCompletionSources;

        private Socket _clientSocket;

        private SpinLock _dataReceivedCallbacksLock;

        private SpinLock _lock;

        private int _port;

        private uint _responseID;
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
        public Client(ushort maxPacketSize = 0)
        {
            _clientSocket = null;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources =
                new Dictionary<uint, TaskCompletionSource<Packet>>(INITIAL_TASKCOMPLETION_QUEUE_SIZE);

            _lock = new SpinLock(Debugger.IsAttached);
            _dataReceivedCallbacksLock = new SpinLock(Debugger.IsAttached);
            _responseID = 1;

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);

            _state = new ClientStateObject(new byte[maxPacketSize > 0 ? maxPacketSize : Constants.PACKET_SIZE_MAX]);
        }

        /// <summary>
        ///     ClientBase destructor
        /// </summary>
        ~Client()
        {
            Dispose(false);
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public bool Connect(SocketMode mode, string serverAddress, int port, int timeout = 10)
        {
            if (_clientSocket != null)
            {
                try
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                    _clientSocket.Close(5000);
                    _clientSocket = null;
                }
                catch
                {
                    /* IGNORE */
                }
            }

            _serverAddress = serverAddress;
            _port = port;

            _manuelResetEvent.Reset();

            switch (mode)
            {
                case SocketMode.Tcp:
                    _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true,
                        Blocking = false
                    };
                    break;
                case SocketMode.Udp:
                    _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                        { Blocking = false };
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(SocketMode));
            }

            try
            {
                IAsyncResult iar = _clientSocket.BeginConnect(Dns.GetHostAddresses(serverAddress), port, null, null);
                bool result = iar.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                _clientSocket.EndConnect(iar);

                if (result)
                {
                    ReceiveAsync();
                    SendConnect();
                    return _manuelResetEvent.WaitOne(timeout * 1000);
                }
            }
            catch
            {
                /* IGNORE */
            }
            return false;
        }

        private void ReceiveAsync()
        {
            try
            {
                _clientSocket.BeginReceive(
                    _state.Buffer, 0, _state.Buffer.Length, SocketFlags.None, ReceiveAsyncCallback, null);
            }
            catch { OnDisconnected(); }
        }

        private void ReceiveAsyncCallback(IAsyncResult iar)
        {
            int length;
            try
            {
                if ((length = _clientSocket.EndReceive(iar)) <= 0)
                {
                    OnDisconnected();
                    return;
                }
            }
            catch
            {
                OnDisconnected();
                return;
            }

            _state.Buffer.GetHeader(out uint commandID, out int dataLength, out uint response, out uint compressed);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if (compressed != 0)
                {
                    int l;
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(_state.Buffer, Constants.HEADER_SIZE);
                        l = BitConverter.ToInt32(_state.Buffer, Constants.HEADER_SIZE + 4);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _state.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        l = BitConverter.ToInt32(_state.Buffer, 0);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _state.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    ReceiveAsync();

                    DeserializeData(commandID, data, 0, l, responseID);
                }
                else
                {
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(_state.Buffer, Constants.HEADER_SIZE);
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                    }

                    ReceiveAsync();

                    DeserializeData(commandID, data, 0, dataLength, responseID);
                }
                return;
            }

            ReceiveAsync();
        }

        private void DeserializeData(uint commandid, byte[] data, int offset, int length, uint responseid)
        {
            if (responseid != 0)
            {
                TaskCompletionSource<Packet> cs;
                bool lockTaken = false;
                try
                {
                    _lock.Enter(ref lockTaken);
                    if (_taskCompletionSources.TryGetValue(responseid, out cs))
                    {
                        _taskCompletionSources.Remove(responseid);
                    }
                }
                finally
                {
                    if (lockTaken) { _lock.Exit(false); }
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
                    PING_STRUCT pingStruct;
                    unsafe
                    {
                        fixed (byte* ptr = data)
                        {
                            pingStruct = *(PING_STRUCT*)(ptr + offset);
                        }
                    }
                    Ping?.Invoke(pingStruct);
                    break;
                }
                case CommandID.CONNECT:
                {
                    data.FromBytesUnsafe(offset, out CONNECT_STRUCT connectStruct);
                    if (connectStruct.Checksum.SequenceEqual(_connectChecksum))
                    {
                        _manuelResetEvent.Set();
                    }
                    break;
                }
                default:
                {
                    if (commandid <= Constants.USER_COMMAND_LIMIT &&
                        _dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry cee))
                    {
                        Packet packet = new Packet(data, offset, length);
                        cee._deserialize.BeginInvoke(
                            in packet, iar =>
                            {
                                object res = cee._deserialize.EndInvoke(in packet, iar);
                                ByteArrayPool.Return(data);

                                if (res != null) { cee.RaiseAsync(this, res); }
                            }, null);
                        return;
                    }
                    break;
                }
            }
            ByteArrayPool.Return(data);
        }

        private void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        #endregion

        #region Nested

        private struct ClientStateObject
        {
            public readonly byte[] Buffer;

            public ClientStateObject(byte[] buffer)
            {
                Buffer = buffer;
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

        /// <inheritdoc />
        public void Send(uint commandid, byte[] data, int offset, int lenght)
        {
            BeginSendData(commandid, data, offset, lenght, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght)
            where TResult : struct
        {
            return SendR(commandid, data, offset, lenght, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght,
            DeserializePacket<TResult> deserialize)
        {
            return SendR(commandid, data, offset, lenght, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght,
            TimeSpan timeout)
            where TResult : struct
        {
            return SendR(commandid, data, offset, lenght, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght,
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
                    _lock.Enter(ref lockTaken);
                    responseID = _responseID++;
                    if (responseID == 0) { responseID++; }
                    _taskCompletionSources.Add(responseID, tcs);
                }
                finally
                {
                    if (lockTaken) { _lock.Exit(false); }
                }

                cts.Token.Register(
                    delegate
                    {
                        bool lockTaken1 = false;
                        try
                        {
                            _lock.Enter(ref lockTaken1);
                            _taskCompletionSources.Remove(_responseID);
                        }
                        finally
                        {
                            if (lockTaken1) { _lock.Exit(false); }
                        }
                        tcs.TrySetResult(new Packet());
                    }, false);
                BeginSendData(commandid, data, offset, lenght, responseID);

                Packet packet = await tcs.Task;
                if (packet.Buffer != null && deserialize != null)
                {
                    TResult result = deserialize(in packet);
                    ByteArrayPool.Return(packet.Buffer);
                    return new Response<TResult>(result);
                }
                return new Response<TResult>();
            }
        }

        /// <inheritdoc />
        public void Send(uint commandid, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize(out int length);
            BeginSendData(commandid, dataB, 0, length, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable)
            where TResult : struct
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
            where TResult : struct
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
        public void Send<T>(uint commandid, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            BeginSendData(commandid, dataB, 0, lenght, 0);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data)
            where T : struct
            where TResult : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            return SendR(commandid, dataB, 0, lenght, DeserializeResponse<TResult>, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data,
            DeserializePacket<TResult> deserialize) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            return SendR(commandid, dataB, 0, lenght, deserialize, s_defaultTimeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data, TimeSpan timeout)
            where T : struct
            where TResult : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            return SendR(commandid, dataB, 0, lenght, DeserializeResponse<TResult>, timeout);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data,
            DeserializePacket<TResult> deserialize, TimeSpan timeout) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            return SendR(commandid, dataB, 0, lenght, deserialize, timeout);
        }

        private void BeginSendData(uint commandid, byte[] data, int offset, int lenght, uint responseID)
        {
            if (_clientSocket == null) { return; }

            Serialization.Serialization.Serialize(
                commandid, data, offset, lenght, responseID, out byte[] send, out int size);

            try
            {
                _clientSocket.BeginSend(
                    send, 0, size, SocketFlags.None, SendDataCallback, send);
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
                if (_clientSocket.EndSend(iar) <= 0)
                {
                    OnDisconnected();
                }
                byte[] send = (byte[])iar.AsyncState;
                ByteArrayPool.Return(send);
            }
            catch
            {
                /* IGNORE */
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult DeserializeResponse<TResult>(in Packet packet)
            where TResult : struct
        {
            return packet.Buffer.FromBytesUnsafe<TResult>(packet.Offset);
        }

        /// <inheritdoc />
        public void SendPing()
        {
            Send(CommandID.PING, new PING_STRUCT { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public Task<Response<PING_STRUCT>> SendRPing()
        {
            return SendR<PING_STRUCT, PING_STRUCT>(
                CommandID.PING, new PING_STRUCT { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public void SendClientInfo(long clientID, string clientName)
        {
            Send(
                CommandID.CLIENTINFO,
                new CLIENTINFO_STRUCT { ClientID = clientID, ClientName = clientName });
        }

        private void SendConnect()
        {
            Send(CommandID.CONNECT, new CONNECT_STRUCT { Checksum = _connectChecksum });
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
                if (disposing)
                {
                    try
                    {
                        if (_clientSocket != null)
                        {
                            Send(CommandID.DISCONNECT, new byte[1] { 255 }, 0, 1);
                            _clientSocket.Shutdown(SocketShutdown.Both);
                            _clientSocket.Close(5000);
                        }
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

        #endregion
    }
}