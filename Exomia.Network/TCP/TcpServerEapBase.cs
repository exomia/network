#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using Exomia.Network.Native;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class TcpServerEapBase<TServerClient> : TcpServerBase<TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <summary>
        ///     Initializes a new instance of the &lt;see cref="TcpServerEapBase&lt;TServerClient&gt;
        ///     "/&gt; class.
        /// </summary>
        /// <param name="expectedMaxClients"> (Optional) The expected maximum clients. </param>
        /// <param name="maxPacketSize">      (Optional) Size of the maximum packet. </param>
        protected TcpServerEapBase(ushort expectedMaxClients = 32, ushort maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
            : base(maxPacketSize)
        {
            _sendEventArgsPool = new SocketAsyncEventArgsPool(expectedMaxClients);
        }

        private protected override unsafe SendError SendTo(Socket arg0,
                                                           uint   commandID,
                                                           byte[] data,
                                                           int    offset,
                                                           int    length,
                                                           uint   responseID)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();

                if (sendEventArgs == null)
                {
                    sendEventArgs           =  new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }

                fixed (byte* src = data)
                fixed (byte* dst = sendEventArgs.Buffer)
                {
                    Serialization.Serialization.SerializeTcp(
                        commandID, src + offset, length, responseID, EncryptionMode.None,
                        CompressionMode.Lz4, dst, out int size);
                    sendEventArgs.SetBuffer(0, size);
                }
                sendEventArgs.AcceptSocket = arg0;

                try
                {
                    if (!arg0.SendAsync(sendEventArgs))
                    {
                        SendAsyncCompleted(arg0, sendEventArgs);
                    }
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Error);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Socket;
                }
                catch
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

        /// <inheritdoc />
        private protected override void ListenAsync()
        {
            SocketAsyncEventArgs acceptArgs = new SocketAsyncEventArgs();
            acceptArgs.Completed += AcceptAsyncCompleted;
            ListenAsync(acceptArgs);
        }

        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
        /// <param name="acceptArgs"> Socket asynchronous event information. </param>
        private void ListenAsync(SocketAsyncEventArgs acceptArgs)
        {
            acceptArgs.AcceptSocket = null;
            try
            {
                if (!_listener.AcceptAsync(acceptArgs))
                {
                    AcceptAsyncCompleted(_listener, acceptArgs);
                }
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <summary>
        ///     Accept asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                try
                {
                    e.AcceptSocket?.Shutdown(SocketShutdown.Both);
                    e.AcceptSocket?.Close(CLOSE_TIMEOUT);
                }
                catch
                {
                    /* IGNORE */
                }
                ListenAsync(e);
                return;
            }
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs { AcceptSocket = e.AcceptSocket };
                receiveArgs.Completed += ReceiveAsyncCompleted;
                receiveArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                receiveArgs.UserToken = new ServerClientStateObject
                {
                    BufferRead = new byte[_maxPacketSize], CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ListenAsync(e);

                ReceiveAsync(receiveArgs);
            }
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        /// <param name="args"> Socket asynchronous event information. </param>
        private void ReceiveAsync(SocketAsyncEventArgs args)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!args.AcceptSocket.ReceiveAsync(args))
                    {
                        ReceiveAsyncCompleted(args.AcceptSocket, args);
                    }
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Unspecified); }
            }
        }

        /// <summary>
        ///     Receive asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
                return;
            }
            int bytesTransferred = e.BytesTransferred;
            if (bytesTransferred <= 0)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Graceful);
                return;
            }

            ServerClientStateObject state = (ServerClientStateObject)e.UserToken;

            Receive(e.AcceptSocket, state.CircularBuffer, e.Buffer, state.BufferRead, bytesTransferred);

            ReceiveAsync(e);
        }

        /// <summary>
        ///     Sends an asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void SendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer read.
            /// </summary>
            public byte[] BufferRead;

            /// <summary>
            ///     Buffer for circular data.
            /// </summary>
            public CircularBuffer CircularBuffer;
        }
    }
}