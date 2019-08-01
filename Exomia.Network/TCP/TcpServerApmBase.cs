#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
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
        /// <param name="maxPacketSize"> (Optional) Size of the maximum packet. </param>
        protected TcpServerApmBase(ushort maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
            : base(maxPacketSize) { }

        private protected override unsafe SendError SendTo(Socket arg0,
                                                           int    packetID,
                                                           uint   commandID,
                                                           uint   responseID,
                                                           byte*  src,
                                                           int    chunkLength,
                                                           int    chunkOffset,
                                                           int    length)
        {
            SendStateObject state;
            state.Buffer = ByteArrayPool.Rent(Constants.TCP_HEADER_OFFSET + length + 1);
            state.Socket = arg0;
            int size;
            fixed (byte* dst = state.Buffer)
            {
                size = Serialization.Serialization.SerializeTcp(
                    packetID, commandID, responseID,
                    src, dst, chunkLength, chunkOffset, length,
                    _encryptionMode, _compressionMode);
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
            try
            {
                _listener.BeginAccept(AcceptCallback, null);
            }
            catch
            {
                /* IGNORE */
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
                Socket socket = _listener.EndAccept(ar);
                ServerClientStateObject state = new ServerClientStateObject
                {
                    //0.2mb
                    Socket         = socket,
                    BufferWrite    = new byte[_maxPacketSize],
                    BufferRead     = new byte[_maxPacketSize],
                    CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ReceiveAsync(state);
            }
            catch (ObjectDisposedException)
            {
                /* SOCKET CLOSED */
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
        private void ReceiveAsync(ServerClientStateObject state)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    state.Socket.BeginReceive(
                        state.BufferWrite, 0, state.BufferWrite.Length, SocketFlags.None, ReceiveDataCallback,
                        state);
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified); }
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
            int                     bytesTransferred;
            try
            {
                if ((bytesTransferred = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Error);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                return;
            }

            if (Serialization.Serialization.DeserializeTcp(
                state.CircularBuffer, state.BufferWrite, state.BufferRead, bytesTransferred,
                _bigDataHandler,
                out uint commandID, out uint responseID, out byte[] data, out int dataLength))
            {
                ReceiveAsync(state);
                DeserializeData(state.Socket, commandID, data, 0, dataLength, responseID);
                return;
            }

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
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer write.
            /// </summary>
            public byte[] BufferWrite;

            /// <summary>
            ///     The buffer read.
            /// </summary>
            public byte[] BufferRead;

            /// <summary>
            ///     Buffer for circular data.
            /// </summary>
            public CircularBuffer CircularBuffer;

            /// <summary>
            ///     The socket.
            /// </summary>
            public Socket Socket;
        }
    }
}