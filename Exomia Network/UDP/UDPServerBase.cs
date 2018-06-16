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

        #endregion

        #region Constructors

        /// <inheritdoc />
        protected UdpServerBase(uint maxClients, int maxPacketSize = Constants.PACKET_SIZE_MAX)
        {
            _pool = new ServerClientStateObjectPool(maxClients, maxPacketSize);
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    { Blocking = false };
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
                    ReceiveDataCallback, state);
            }
            catch (ObjectDisposedException) { InvokeClientDisconnect(state.EndPoint, DisconnectReason.Aborted); }
            catch { InvokeClientDisconnect(state.EndPoint, DisconnectReason.Error); }
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
                return;
            }
            catch (ObjectDisposedException) { InvokeClientDisconnect(arg0, DisconnectReason.Aborted); }
            catch { InvokeClientDisconnect(arg0, DisconnectReason.Error); }

            ByteArrayPool.Return(send);
        }

        private void SendDataToCallback(IAsyncResult iar)
        {
            try
            {
                _listener.EndSendTo(iar);
            }
            finally
            {
                byte[] send = (byte[])iar.AsyncState;
                ByteArrayPool.Return(send);
            }
        }

        private unsafe void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;

            int length;
            try
            {
                if ((length = _listener.EndReceiveFrom(iar, ref state.EndPoint)) <= 0)
                {
                    InvokeClientDisconnect(state.EndPoint, DisconnectReason.Unspecified);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Aborted);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Error);
                return;
            }

            ListenAsync();

            state.Buffer.GetHeader(out uint commandID, out int dataLength, out byte h1);
            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if ((h1 & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                {
                    int l;
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                            l = *(int*)(ptr + Constants.HEADER_SIZE + 4);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            l = *(int*)(ptr + Constants.HEADER_SIZE);
                        }
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    DeserializeData(state.EndPoint, commandID, data, 0, l, responseID);
                }
                else
                {
                    if ((h1 & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        fixed (byte* ptr = state.Buffer)
                        {
                            responseID = *(uint*)(ptr + Constants.HEADER_SIZE);
                        }
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

            public ServerClientStateObjectPool(uint maxClients, int maxPacketSize)
            {
                _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.PACKET_SIZE_MAX
                    ? maxPacketSize
                    : Constants.PACKET_SIZE_MAX;
                _lock = new SpinLock(Debugger.IsAttached);
                _buffers = new ServerClientStateObject[maxClients != 0 ? maxClients + 1u : 33];
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