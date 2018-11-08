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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Native;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network.UDP
{
    /// <inheritdoc />
    /// <summary>
    ///     A UDP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient">TServerClient</typeparam>
    public abstract class UdpServerApmBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        private readonly ServerClientStateObjectPool _pool;

        /// <inheritdoc />
        protected UdpServerApmBase(uint maxClients, int maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _pool = new ServerClientStateObjectPool(maxClients, maxPacketSize);
        }

        /// <inheritdoc />
        public override SendError SendTo(EndPoint arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.SerializeUdp(
                    commandid, data, offset, length, responseid, EncryptionMode.None, out byte[] send, out int size);
                try
                {
                    _listener.BeginSendTo(send, 0, size, SocketFlags.None, arg0, SendDataToCallback, send);
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                    ByteArrayPool.Return(send);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Error);
                    ByteArrayPool.Return(send);
                    return SendError.Socket;
                }
                catch
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                    ByteArrayPool.Return(send);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

        private protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false, DualMode = true
                    };
                    listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false
                    };
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
        }

        private protected override void ListenAsync()
        {
            ServerClientStateObject state = _pool.Rent();
            try
            {
                _listener.BeginReceiveFrom(
                    state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref state.EndPoint,
                    ReceiveDataCallback, state);
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Aborted);
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Error);
            }
            catch { InvokeClientDisconnect(state.EndPoint, DisconnectReason.Unspecified); }
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
                    InvokeClientDisconnect(state.EndPoint, DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Error);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Unspecified);
                return;
            }

            ListenAsync();

            state.Buffer.GetHeaderUdp(out byte packetHeader, out uint commandID, out int dataLength);

            if (length == dataLength + Constants.UDP_HEADER_SIZE)
            {
                EndPoint ep = state.EndPoint;

                uint responseID = 0;
                int offset = 0;
                fixed (byte* src = state.Buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset     = 4;
                    }
                    byte[] payload;
                    if ((packetHeader & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                    {
                        int l = *(int*)(src + offset);
                        offset += 4;

                        payload = ByteArrayPool.Rent(l);
                        int s = LZ4Codec.Decode(
                            state.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                        DeserializeData(ep, commandID, payload, 0, l, responseID);
                    }
                    else
                    {
                        dataLength -= offset;
                        payload    =  ByteArrayPool.Rent(dataLength);

                        fixed (byte* dest = payload)
                        {
                            Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                        }

                        DeserializeData(ep, commandID, payload, 0, dataLength, responseID);
                    }
                }
            }

            _pool.Return(state);
        }

        private sealed class ServerClientStateObject
        {
            public byte[] Buffer;
            public EndPoint EndPoint;
        }

        private class ServerClientStateObjectPool
        {
            private readonly ServerClientStateObject[] _buffers;
            private readonly int _maxPacketSize;

            private int _index;
            private SpinLock _lock;

            public ServerClientStateObjectPool(uint maxClients, int maxPacketSize)
            {
                _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                    ? maxPacketSize
                    : Constants.UDP_PACKET_SIZE_MAX;
                _lock    = new SpinLock(System.Diagnostics.Debugger.IsAttached);
                _buffers = new ServerClientStateObject[maxClients != 0 ? maxClients + 1u : 33];
            }

            internal ServerClientStateObject Rent()
            {
                ServerClientStateObject buffer = null;
                bool lockTaken = false;
                try
                {
                    _lock.Enter(ref lockTaken);

                    if (_index < _buffers.Length)
                    {
                        buffer             = _buffers[_index];
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
                    Buffer = new byte[_maxPacketSize], EndPoint = new IPEndPoint(IPAddress.Any, 0)
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
                        obj.EndPoint       = new IPEndPoint(IPAddress.Any, 0);
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
        }
    }
}