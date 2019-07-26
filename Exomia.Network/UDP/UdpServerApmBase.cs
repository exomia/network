#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class UdpServerApmBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        /// <summary>
        ///     The pool.
        /// </summary>
        private readonly ServerClientStateObjectPool _pool;

        /// <inheritdoc />
        protected UdpServerApmBase(uint maxClients, int maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _pool = new ServerClientStateObjectPool(maxClients, maxPacketSize);
        }

        private protected override unsafe SendError SendTo(EndPoint arg0,
                                                           uint     commandid,
                                                           byte[]   data,
                                                           int      offset,
                                                           int      length,
                                                           uint     responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                byte[] send;
                int    size;
                fixed (byte* src = data)
                {
                    Serialization.Serialization.SerializeUdp(
                        commandid, src + offset, length, responseid, EncryptionMode.None, out send,
                        out size);
                }

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

        /// <summary>
        ///     Executes the run action.
        /// </summary>
        /// <param name="port">     The port. </param>
        /// <param name="listener"> [out] The listener. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
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

        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
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

        /// <summary>
        ///     Async callback, called on completion of send data to callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
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

        /// <summary>
        ///     Async callback, called on completion of receive data callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
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
                int  offset     = 0;
                fixed (byte* src = state.Buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset     = 4;
                    }
                    byte[] payload;
                    switch ((CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK))
                    {
                        case CompressionMode.Lz4:
                            int l = *(int*)(src + offset);
                            offset += 4;

                            payload = ByteArrayPool.Rent(l);
                            int s = LZ4Codec.Decode(
                                state.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l);
                            if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                            DeserializeData(ep, commandID, payload, 0, l, responseID);
                            break;
                        case CompressionMode.None:
                        default:
                            dataLength -= offset;
                            payload    =  ByteArrayPool.Rent(dataLength);

                            fixed (byte* dest = payload)
                            {
                                Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }

                            DeserializeData(ep, commandID, payload, 0, dataLength, responseID);
                            break;
                    }
                }
            }

            _pool.Return(state);
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer.
            /// </summary>
            public byte[] Buffer;

            /// <summary>
            ///     The end point.
            /// </summary>
            public EndPoint EndPoint;
        }

        /// <summary>
        ///     A server client state object pool.
        /// </summary>
        private class ServerClientStateObjectPool
        {
            /// <summary>
            ///     The buffers.
            /// </summary>
            private readonly ServerClientStateObject[] _buffers;

            /// <summary>
            ///     Size of the maximum packet.
            /// </summary>
            private readonly int _maxPacketSize;

            /// <summary>
            ///     The index.
            /// </summary>
            private int _index;

            /// <summary>
            ///     The lock.
            /// </summary>
            private SpinLock _lock;

            /// <summary>
            ///     Initializes a new instance of the <see cref="ServerClientStateObjectPool" /> class.
            /// </summary>
            /// <param name="maxClients">    The maximum clients. </param>
            /// <param name="maxPacketSize"> Size of the maximum packet. </param>
            public ServerClientStateObjectPool(uint maxClients, int maxPacketSize)
            {
                _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                    ? maxPacketSize
                    : Constants.UDP_PACKET_SIZE_MAX;
                _lock    = new SpinLock(Debugger.IsAttached);
                _buffers = new ServerClientStateObject[maxClients != 0 ? maxClients + 1u : 33];
            }

            /// <summary>
            ///     Gets the rent.
            /// </summary>
            /// <returns>
            ///     A ServerClientStateObject.
            /// </returns>
            internal ServerClientStateObject Rent()
            {
                ServerClientStateObject buffer    = null;
                bool                    lockTaken = false;
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

            /// <summary>
            ///     Returns the given object.
            /// </summary>
            /// <param name="obj"> The Object to return. </param>
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