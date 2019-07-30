#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Network.Buffers;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class UdpServerApmBase<TServerClient> : UdpServerBase<TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        /// <summary>
        ///     The pool.
        /// </summary>
        private readonly ObjectPool<ServerClientStateObject> _serverClientStateObjectPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpServerApmBase{TServerClien}" /> class.
        /// </summary>
        /// <param name="expectedMaxClients"> The expected maximum clients. </param>
        /// <param name="maxPacketSize">      (Optional) Size of the maximum packet. </param>
        protected UdpServerApmBase(ushort expectedMaxClients, ushort maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
            : base(maxPacketSize)
        {
            _serverClientStateObjectPool = new ObjectPool<ServerClientStateObject>(expectedMaxClients);
        }

        private protected override unsafe SendError SendTo(EndPoint arg0,
                                                           uint     commandID,
                                                           byte[]   data,
                                                           int      offset,
                                                           int      length,
                                                           uint     responseID)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                byte[] send;
                int    size;
                fixed (byte* src = data)
                {
                    Serialization.Serialization.SerializeUdp(
                        commandID, src + offset, length, responseID, EncryptionMode.None,
                        CompressionMode.Lz4, out send, out size);
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
        ///     Listen asynchronous.
        /// </summary>
        private protected override void ListenAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                ServerClientStateObject state = _serverClientStateObjectPool.Rent()
                                             ?? new ServerClientStateObject(new byte[_maxPacketSize]);
                state.EndPoint = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    _listener.BeginReceiveFrom(
                        state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ref state.EndPoint,
                        ReceiveDataCallback, state);
                }
                catch
                {
                    _serverClientStateObjectPool.Return(state);
                }
            }
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
        private void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;

            int bytesTransferred;
            try
            {
                if ((bytesTransferred = _listener.EndReceiveFrom(iar, ref state.EndPoint)) <= 0)
                {
                    InvokeClientDisconnect(state.EndPoint, DisconnectReason.Graceful);
                    _serverClientStateObjectPool.Return(state);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Aborted);
                _serverClientStateObjectPool.Return(state);
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Error);
                _serverClientStateObjectPool.Return(state);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.EndPoint, DisconnectReason.Unspecified);
                _serverClientStateObjectPool.Return(state);
                return;
            }

            ListenAsync();

            Receive(state.Buffer, bytesTransferred, state.EndPoint);

            _serverClientStateObjectPool.Return(state);
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer.
            /// </summary>
            public readonly byte[] Buffer;

            /// <summary>
            ///     The end point.
            /// </summary>
            public EndPoint EndPoint;

            /// <summary>
            ///     Initializes a new instance of the <see cref="ServerClientStateObject" /> class.
            /// </summary>
            /// <param name="buffer"> The buffer. </param>
            public ServerClientStateObject(byte[] buffer)
            {
                Buffer = buffer;
            }
        }
    }
}