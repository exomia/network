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
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Lib;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <inheritdoc />
    public abstract class ClientBase : IClient, IDisposable
    {
        #region Variables

        private const int INITIAL_QUEUE_SIZE = 16;
        private const int INITIAL_TASKCOMPLETION_QUEUE_SIZE = 128;
        /// <summary>
        ///     called than the client is Disconnected
        /// </summary>
        public event DisconnectedHandler Disconnected;

        private readonly Dictionary<uint, ClientEventEntry> _dataReceivedCallbacks;

        private readonly Dictionary<uint, TaskCompletionSource<byte[]>> _taskCompletionSources;

        /// <summary>
        ///     Socket
        /// </summary>
        protected Socket _clientSocket;

        private int _port;
        private string _serverAddress;

        private int _responseID;

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
        protected ClientBase()
        {
            _clientSocket = null;
            _dataReceivedCallbacks = new Dictionary<uint, ClientEventEntry>(INITIAL_QUEUE_SIZE);
            _taskCompletionSources = new Dictionary<uint, TaskCompletionSource<byte[]>>(INITIAL_TASKCOMPLETION_QUEUE_SIZE);
            _responseID = 1;
        }

        /// <summary>
        ///     ClientBase destructor
        /// </summary>
        ~ClientBase()
        {
            Dispose(false);
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public bool Connect(string serverAddress, int port, int timeout = 10)
        {
            if (_clientSocket != null) { return true; }

            _serverAddress = serverAddress;
            _port = port;

            return OnConnect(serverAddress, port, timeout, out _clientSocket);
        }

        /// <summary>
        ///     called than a client wants to connect with a server
        /// </summary>
        /// <param name="serverAddress">serverAddress</param>
        /// <param name="port">port</param>
        /// <param name="timeout">timeout</param>
        /// <param name="socket">out socket</param>
        /// <returns></returns>
        protected abstract bool OnConnect(string serverAddress, int port, int timeout, out Socket socket);

        /// <summary>
        ///     call to deserialize the data async
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="length">data length</param>
        /// <param name="responseid">response id</param>
        protected async void DeserializeDataAsync(uint commandid, uint type, byte[] data, int length, uint responseid)
        {
            if (responseid != 0)
            {
                if (_taskCompletionSources.TryGetValue(responseid, out TaskCompletionSource<byte[]> cs))
                {
                    cs.SetResult(data);
                }
                return;
            }

            if (!_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                return;
            }

            object result = null;
            switch (commandid)
            {
                case Constants.PING_COMMAND_ID:
                {
                    unsafe
                    {
                        fixed (byte* ptr = data)
                        {
                            result = *(PING_STRUCT*)ptr;
                        }
                    }
                    break;
                }
                case Constants.UDP_CONNECT_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out UDP_CONNECT_STRUCT connectStruct);
                    result = connectStruct;
                    break;
                }

                case Constants.CLIENTINFO_COMMAND_ID:
                {
                    data.FromBytesUnsafe(out CLIENTINFO_STRUCT clientinfoStruct);
                    result = clientinfoStruct;
                    break;
                }
                default:
                {
                    result = await Task.Run(delegate { return DeserializeData(type, data, length); });
                    break;
                }
            }

            if (result == null) { return; }

            buffer.RaiseAsync(this, result);


        }

        /// <summary>
        ///     deserialize data from type and byte array
        /// </summary>
        /// <param name="type">type</param>
        /// <param name="data">byte array</param>
        /// <param name="length">data length</param>
        /// <returns>a new created object</returns>
        protected abstract object DeserializeData(uint type, byte[] data, int length);

        /// <summary>
        ///     OnDisconnected called if the client is disconnected
        /// </summary>
        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        #endregion

        #region Add & Remove

        /// <summary>
        ///     remove a command
        /// </summary>
        /// <param name="commandid">command id</param>
        public void RemoveCommand(uint commandid)
        {
            if (_dataReceivedCallbacks.ContainsKey(commandid))
            {
                _dataReceivedCallbacks.Remove(commandid);
            }
        }

        /// <summary>
        ///     add a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">callback</param>
        public void AddDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                buffer.Add(callback);
            }
            else
            {
                ClientEventEntry entry = new ClientEventEntry();
                entry.Add(callback);
                _dataReceivedCallbacks.Add(commandid, entry);
            }
        }

        /// <summary>
        ///     remove a data received callback
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="callback">DataReceivedHandler</param>
        public void RemoveDataReceivedCallback(uint commandid, DataReceivedHandler callback)
        {
            if (_dataReceivedCallbacks.TryGetValue(commandid, out ClientEventEntry buffer))
            {
                buffer.Remove(callback);
            }
        }

        #endregion

        #region Send

        /// <inheritdoc />
        public void Send(uint commandid, uint type, byte[] data, int lenght)
        {
            BeginSendData(commandid, type, data, lenght);
        }

        /// <inheritdoc />
        public async Task<TResult> SendR<TResult>(uint commandid, uint type, byte[] data, int lenght)
            where TResult : struct
        {
            TaskCompletionSource<byte[]> source = new TaskCompletionSource<byte[]>();

            //TODO maybe use kernel32 instead of Interlocked.Add (x86 & x64 support required)
            uint responseID = (uint)Interlocked.Add(ref _responseID, 1);
            if (responseID == 0) { responseID = (uint)Interlocked.Add(ref _responseID, 1); }

            _taskCompletionSources.Add(responseID, source);
            BeginSendData(commandid, type, data, lenght, responseID);
            return (await source.Task).FromBytesUnsafe<TResult>();
        }

        /// <inheritdoc />
        public void Send(uint commandid, uint type, ISerializable serializable)
        {
            byte[] dataB = serializable.Serialize();
            BeginSendData(commandid, type, dataB, dataB.Length);
        }

        /// <inheritdoc />
        public void SendAsync(uint commandid, uint type, ISerializable serializable)
        {
            Task.Run(
                delegate
                {
                    Send(commandid, type, serializable);
                });
        }

        /// <inheritdoc />
        public void Send<T>(uint commandid, uint type, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            BeginSendData(commandid, type, dataB, lenght);
        }

        /// <inheritdoc />
        public void SendAsync<T>(uint commandid, uint type, in T data) where T : struct
        {
            data.ToBytesUnsafe(out byte[] dataB, out int lenght);
            Task.Run(
                delegate
                {
                    BeginSendData(commandid, type, dataB, lenght);
                });
        }

        private void BeginSendData(uint commandid, uint type, byte[] data, int lenght, uint responseID = 0)
        {
            if (_clientSocket == null) { return; }

            byte[] send = Serialization.Serialization.Serialize(commandid, type, data, lenght, responseID);

            try
            {
                _clientSocket.BeginSend(
                    send, 0, Constants.HEADER_SIZE + lenght, SocketFlags.None, SendDataCallback, send);
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
                _clientSocket.EndSend(iar);
                byte[] send = (byte[])iar.AsyncState;
                ByteArrayPool.Return(send);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        public void SendPing()
        {
            Send(
                Constants.PING_COMMAND_ID, Constants.PING_STRUCT_TYPE_ID,
                new PING_STRUCT { TimeStamp = DateTime.Now.Ticks });
        }

        /// <inheritdoc />
        public void SendClientInfo(long clientID, string clientName)
        {
            Send(
                Constants.CLIENTINFO_COMMAND_ID, Constants.CLIENTINFO_STRUCT_TYPE_ID,
                new CLIENTINFO_STRUCT { ClientID = clientID, ClientName = clientName });
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
                        _clientSocket?.Shutdown(SocketShutdown.Both);
                        _clientSocket?.Close(5000);
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

        /// <summary>
        ///     OnDispose
        /// </summary>
        /// <param name="disposing">disposing</param>
        protected virtual void OnDispose(bool disposing) { }

        #endregion
    }
}