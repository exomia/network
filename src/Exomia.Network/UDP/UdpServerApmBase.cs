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
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        protected UdpServerApmBase(ushort expectedMaxClients,
                                   ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _serverClientStateObjectPool = new ObjectPool<ServerClientStateObject>(expectedMaxClients);
        }

        private protected override unsafe SendError SendTo(EndPoint      arg0,
                                                           in PacketInfo packetInfo)
        {
            int    size;
            byte[] buffer = ByteArrayPool.Rent(Constants.UDP_HEADER_OFFSET + packetInfo.ChunkLength);
            fixed (byte* dst = buffer)
            {
                size = Serialization.Serialization.SerializeUdp(in packetInfo, dst, _encryptionMode);
            }

            try
            {
                _listener.BeginSendTo(buffer, 0, size, SocketFlags.None, arg0, SendDataToCallback, buffer);
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                ByteArrayPool.Return(buffer);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Error);
                ByteArrayPool.Return(buffer);
                return SendError.Socket;
            }
            catch
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                ByteArrayPool.Return(buffer);
                return SendError.Unknown;
            }
        }

        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
        private protected override void ListenAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                ServerClientStateObject state = _serverClientStateObjectPool.Rent()
                                             ?? new ServerClientStateObject(
                                                    new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET]);
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

            if (Serialization.Serialization.DeserializeUdp(
                state.Buffer, bytesTransferred, _bigDataHandler,
                out DeserializePacketInfo deserializePacketInfo))
            {
                DeserializeData(state.EndPoint, in deserializePacketInfo);
            }

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