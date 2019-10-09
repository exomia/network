#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using System.Runtime.Serialization;
using Exomia.Network.Buffers;
using Exomia.Network.Native;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class TcpServerApmBase<TServerClient> : TcpServerBase<TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerApmBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        protected TcpServerApmBase(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize) { }

        private protected override unsafe SendError SendTo(Socket        arg0,
                                                           in PacketInfo packetInfo)
        {
            SendStateObject state;
            state.Buffer = ByteArrayPool.Rent(Constants.TCP_HEADER_OFFSET + packetInfo.ChunkLength + 1);
            state.Socket = arg0;
            int size;
            fixed (byte* dst = state.Buffer)
            {
                size = Serialization.Serialization.SerializeTcp(in packetInfo, dst, _encryptionMode);
            }

            try
            {
                arg0.BeginSend(state.Buffer, 0, size, SocketFlags.None, BeginSendCallback, state);
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                ByteArrayPool.Return(state.Buffer);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Error);
                ByteArrayPool.Return(state.Buffer);
                return SendError.Socket;
            }
            catch
            {
                InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                ByteArrayPool.Return(state.Buffer);
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
                try
                {
                    _listener!.BeginAccept(AcceptCallback, null);
                }
                catch
                {
                    /* IGNORE */
                }
            }
        }

        /// <summary>
        ///     Async callback, called on completion of begin send callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        private void BeginSendCallback(IAsyncResult iar)
        {
            SendStateObject state = (SendStateObject)iar.AsyncState;
            try
            {
                if (state.Socket.EndSend(iar) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                }
            }
            catch
            {
                /* IGNORE */
            }
            finally
            {
                ByteArrayPool.Return(state.Buffer);
            }
        }

        /// <summary>
        ///     Async callback, called on completion of accept callback.
        /// </summary>
        /// <param name="ar"> The archive. </param>
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = _listener!.EndAccept(ar);
#pragma warning disable IDE0068 // Use recommended dispose pattern
                ServerClientStateObjectApm state = new ServerClientStateObjectApm(
                    new byte[_payloadSize + Constants.TCP_HEADER_OFFSET],
                    new CircularBuffer((_payloadSize + Constants.TCP_HEADER_OFFSET) * 2),
                    new BigDataHandler(),
                    socket,
                    new byte[_payloadSize + Constants.TCP_HEADER_OFFSET]);
#pragma warning restore IDE0068 // Use recommended dispose pattern
                ReceiveAsync(state);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch
            {
                /* IGNORE */
            }

            ListenAsync();
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        /// <param name="state"> The state. </param>
        private void ReceiveAsync(ServerClientStateObjectApm state)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    state.Socket.BeginReceive(
                        state.BufferWrite, 0, state.BufferWrite.Length, SocketFlags.None, ReceiveDataCallback,
                        state);
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted);
                    state.Dispose();
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Error);
                    state.Dispose();
                }
                catch
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                    state.Dispose();
                }
            }
        }

        /// <summary>
        ///     Async callback, called on completion of receive data callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObjectApm state = (ServerClientStateObjectApm)iar.AsyncState;
            int                        bytesTransferred;
            try
            {
                if ((bytesTransferred = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Graceful);
                    state.Dispose();
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted);
                state.Dispose();
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Error);
                state.Dispose();
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                state.Dispose();
                return;
            }

            Receive(state.Socket, state.BufferWrite, bytesTransferred, state);
            ReceiveAsync(state);
        }

        /// <summary>
        ///     A send state object.
        /// </summary>
        private struct SendStateObject
        {
            /// <summary>
            ///     The buffer.
            /// </summary>
            public byte[] Buffer;

            /// <summary>
            ///     The socket.
            /// </summary>
            public Socket Socket;
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObjectApm : ServerClientStateObject
        {
            /// <summary>
            ///     The socket.
            /// </summary>
            public Socket Socket { get; }

            /// <summary>
            ///     The buffer write.
            /// </summary>
            public byte[] BufferWrite { get; }

            /// <summary>
            ///		Initializes a new instance of the <see cref="ServerClientStateObjectApm" /> class.
            /// </summary>
            public ServerClientStateObjectApm(byte[]         bufferRead,
                                              CircularBuffer circularBuffer,
                                              BigDataHandler bigDataHandler,
                                              Socket         socket,
                                              byte[]         bufferWrite)
                : base(bufferRead, circularBuffer, bigDataHandler)
            {
                Socket      = socket;
                BufferWrite = bufferWrite;
            }

            /// <inheritdoc />
            protected override void OnDispose(bool disposing)
            {
                try
                {
                    Socket.Dispose();
                }
                catch
                {
                    /*IGNORE*/
                }
            }
        }
    }
}