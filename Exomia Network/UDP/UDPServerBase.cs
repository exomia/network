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

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network.UDP
{
    /// <inheritdoc />
    public abstract class UdpServerBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        #region Variables

        private readonly ServerClientStateObjectPool _pool;

        /// <summary>
        ///     _max_idle_time
        /// </summary>
        protected double _maxIdleTime;

        #endregion

        #region Constructors

        /// <inheritdoc />
        protected UdpServerBase(int maxClients)
            : this(maxClients, Constants.PACKET_SIZE_MAX, Constants.UDP_IDLE_TIME) { }

        /// <inheritdoc />
        protected UdpServerBase(int maxClients, int maxPacketSize)
            : this(maxClients, maxPacketSize, Constants.UDP_IDLE_TIME) { }

        /// <inheritdoc />
        protected UdpServerBase(int maxClients, double maxIdleTime)
            : this(maxClients, Constants.PACKET_SIZE_MAX, maxIdleTime) { }

        /// <inheritdoc />
        protected UdpServerBase(int maxClients, int maxPacketSize, double maxIdleTime)
        {
            if (maxIdleTime <= 0) { maxIdleTime = Constants.UDP_IDLE_TIME; }
            _maxIdleTime = maxIdleTime;

            _pool = new ServerClientStateObjectPool(maxClients, maxPacketSize);
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                listener.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
        }

        /// <inheritdoc />
        protected override void ListenAsync()
        {
            ServerClientStateObject state = _pool.Rent();
            try
            {
                _listener.BeginReceiveFrom(
                    state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref state.EndPoint,
                    ClientReceiveDataCallback, state);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        protected override void BeginSendDataTo(EndPoint arg0, byte[] send, int offset, int lenght)
        {
            if (_listener == null)
            {
                ByteArrayPool.Return(send);
                return;
            }
            try
            {
                _listener.BeginSendTo(send, offset, lenght, SocketFlags.None, arg0, SendDataToCallback, send);
            }
            catch
            {
                ByteArrayPool.Return(send);
            }
        }

        /// <inheritdoc />
        internal override void OnDefaultCommand(EndPoint arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
        }

        private void SendDataToCallback(IAsyncResult iar)
        {
            try
            {
                _listener.EndSendTo(iar);
                byte[] send = (byte[])iar.AsyncState;
                ByteArrayPool.Return(send);
            }
            catch
            {
                /* IGNORE */
            }
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;

            int length;
            try
            {
                if ((length = _listener.EndReceiveFrom(iar, ref state.EndPoint)) <= 0)
                {
                    InvokeClientDisconnected(state.EndPoint);
                    return;
                }
            }
            catch
            {
                InvokeClientDisconnected(state.EndPoint);
                return;
            }

            ListenAsync();

            state.Buffer.GetHeader(out uint commandID, out int dataLength, out uint response, out uint compressed);
            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if (compressed != 0)
                {
                    int l;
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(state.Buffer, Constants.HEADER_SIZE);
                        l = BitConverter.ToInt32(state.Buffer, Constants.HEADER_SIZE + 4);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        l = BitConverter.ToInt32(state.Buffer, 0);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    DeserializeData(state.EndPoint, commandID, data, 0, l, responseID);
                }
                else
                {
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(state.Buffer, Constants.HEADER_SIZE);
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                    }

                    DeserializeData(state.EndPoint, commandID, data, 0, dataLength, responseID);
                }
            }

            _pool.Return(state);
        }

        #endregion

        #region Nested

        private sealed class ServerClientStateObject
        {
            #region Variables

            public byte[] Buffer;
            public EndPoint EndPoint;

            #endregion
        }

        private class ServerClientStateObjectPool
        {
            #region Variables

            private readonly ServerClientStateObject[] _buffers;
            private readonly int _maxPacketSize;
            private int _index;
            private SpinLock _lock;

            #endregion

            #region Constructors

            public ServerClientStateObjectPool(int maxClients, int maxPacketSize)
            {
                _maxPacketSize = maxPacketSize;
                _lock = new SpinLock(Debugger.IsAttached);
                _buffers = new ServerClientStateObject[maxClients + 1];
            }

            #endregion

            #region Methods

            internal ServerClientStateObject Rent()
            {
                ServerClientStateObject buffer = null;
                bool lockTaken = false;
                try
                {
                    _lock.Enter(ref lockTaken);

                    if (_index < _buffers.Length)
                    {
                        buffer = _buffers[_index];
                        _buffers[_index++] = null;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        _lock.Exit(false);
                    }
                }

                return buffer ?? new ServerClientStateObject
                {
                    Buffer = new byte[_maxPacketSize],
                    EndPoint = new IPEndPoint(IPAddress.Any, 0)
                };
            }

            internal void Return(ServerClientStateObject obj)
            {
                bool lockTaken = false;
                try
                {
                    _lock.Enter(ref lockTaken);

                    if (_index != 0)
                    {
                        obj.EndPoint = new IPEndPoint(IPAddress.Any, 0);
                        _buffers[--_index] = obj;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        _lock.Exit(false);
                    }
                }
            }

            #endregion
        }

        #endregion
    }
}