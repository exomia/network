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
    /// <summary>
    ///     A TCP/UDP-Client build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    public sealed class ClientApm : IClient
    {
        #region Variables

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_TASKCOMPLETION_QUEUE_SIZE = 128;

        private const int CLOSE_TIMEOUT = 10;

        private const byte RECEIVE_FLAG = 0b0000_0001;
        private const byte SEND_FLAG = 0b0000_0010;

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

        private readonly ClientStateObject _stateObj;

        private readonly Dictionary<uint, TaskCompletionSource<Packet>> _taskCompletionSources;

        private Socket _clientSocket;

        private SpinLock _dataReceivedCallbacksLock;

        private SpinLock _lockTaskCompletionSources;

        private int _port;
        private uint _responseID;
        private string _serverAddress;

        private byte _state;

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
        public ClientApm(ushort maxPacketSize = 0)
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

            _stateObj = new ClientStateObject(
                new byte[(maxPacketSize > 0) & (maxPacketSize < Constants.PACKET_SIZE_MAX)
                    ? maxPacketSize
                    : Constants.PACKET_SIZE_MAX]);
        }

        /// <summary>
        ///     ClientBase destructor
        /// </summary>
        ~ClientApm()
        {
            Dispose(false);
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public bool Connect(SocketMode mode, string serverAddress, int port, int timeout = 10)
        {
            Disconnect(DisconnectReason.Graceful);

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
            return false;
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            Disconnect(DisconnectReason.Graceful);
        }

        private void Disconnect(DisconnectReason reason)
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

        private void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    _clientSocket.BeginReceive(
                        _stateObj.Buffer, 0, _stateObj.Buffer.Length, SocketFlags.None, ReceiveAsyncCallback, null);
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        private unsafe void ReceiveAsyncCallback(IAsyncResult iar)
        {
            int length;
            try
            {
                if ((length = _clientSocket.EndReceive(iar)) <= 0)
                {
                    Disconnect(DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                Disconnect(DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                Disconnect(DisconnectReason.Error);
                return;
            }
            catch
            {
                Disconnect(DisconnectReason.Unspecified);
                return;
            }

            _stateObj.Buffer.GetHeader(out uint commandID, out int dataLength, out byte h1);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if ((h1 & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                {
                    int l;
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = _stateObj.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                            l = *(int*)(ptr + Constants.HEADER_SIZE + 4);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _stateObj.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        fixed (byte* ptr = _stateObj.Buffer)
                        {
                            l = *(int*)(ptr + Constants.HEADER_SIZE);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _stateObj.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    ReceiveAsync();
                    DeserializeData(commandID, data, 0, l, responseID);
                }
                else
                {
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = _stateObj.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                        }
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_stateObj.Buffer, Constants.HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_stateObj.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
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
        public SendError Send(uint commandid, byte[] data, int offset, int lenght)
        {
            return BeginSendData(commandid, data, offset, lenght, 0);
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

                SendError sendError = BeginSendData(commandid, data, offset, lenght, responseID);
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
        public SendError Send<T>(uint commandid, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            return BeginSendData(commandid, dataB, 0, lenght, 0);
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

        private SendError BeginSendData(uint commandid, byte[] data, int offset, int lenght, uint responseID)
        {
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.Serialize(
                    commandid, data, offset, lenght, responseID, EncryptionMode.None, out byte[] send, out int size);

                try
                {
                    _clientSocket.BeginSend(
                        send, 0, size, SocketFlags.None, SendDataCallback, send);
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Aborted);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Error);
                    return SendError.Socket;
                }
                catch
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Unspecified);
                    return SendError.Unknown;
                }
            }

            return SendError.Invalid;
        }

        private void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                if (_clientSocket.EndSend(iar) <= 0)
                {
                    Disconnect(DisconnectReason.Error);
                }
            }
            catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
            catch (SocketException) { Disconnect(DisconnectReason.Error); }
            catch { Disconnect(DisconnectReason.Unspecified); }

            byte[] send = (byte[])iar.AsyncState;
            ByteArrayPool.Return(send);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult DeserializeResponse<TResult>(in Packet packet)
            where TResult : struct
        {
            return packet.Buffer.FromBytesUnsafe<TResult>(packet.Offset);
        }

        /// <inheritdoc />
        public SendError SendPing()
        {
            return Send(CommandID.PING, new PING_STRUCT { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public Task<Response<PING_STRUCT>> SendRPing()
        {
            return SendR<PING_STRUCT, PING_STRUCT>(
                CommandID.PING, new PING_STRUCT { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public SendError SendClientInfo(long clientID, string clientName)
        {
            return Send(
                CommandID.CLIENTINFO,
                new CLIENTINFO_STRUCT { ClientID = clientID, ClientName = clientName });
        }

        private SendError SendConnect()
        {
            return Send(CommandID.CONNECT, new CONNECT_STRUCT { Checksum = _connectChecksum });
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
                    Disconnect(DisconnectReason.Graceful);
                }
                _disposed = true;
            }
        }

        #endregion
    }
}