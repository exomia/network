﻿#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerEapBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        protected TcpServerEapBase(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _sendEventArgsPool = new SocketAsyncEventArgsPool(0xFF);
        }

        private void ListenAsync(SocketAsyncEventArgs acceptArgs)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                acceptArgs.AcceptSocket = null;
                try
                {
                    if (!_listener!.AcceptAsync(acceptArgs))
                    {
                        AcceptAsyncCompleted(_listener, acceptArgs);
                    }
                }
                catch
                {
                    /* IGNORE */
                }
            }
        }

        private void AcceptAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                try
                {
                    e.AcceptSocket?.Shutdown(SocketShutdown.Both);
                    e.AcceptSocket?.Close(CLOSE_TIMEOUT);
                    e.AcceptSocket?.Dispose();
                }
                catch
                {
                    /* IGNORE */
                }
                ListenAsync(e);
                return;
            }

#pragma warning disable IDE0068 // Use recommended dispose pattern
            SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs { AcceptSocket = e.AcceptSocket };
#pragma warning restore IDE0068 // Use recommended dispose pattern
            receiveArgs.Completed += ReceiveAsyncCompleted;
            receiveArgs.SetBuffer(
                new byte[_payloadSize + Constants.TCP_HEADER_OFFSET], 0,
                _payloadSize + Constants.TCP_HEADER_OFFSET);
            receiveArgs.UserToken = new ServerClientStateObject(
                new byte[_payloadSize + Constants.TCP_HEADER_OFFSET],
                new CircularBuffer((_payloadSize + Constants.TCP_HEADER_OFFSET) * 2),
                new BigDataHandler<int>.Default());

            ListenAsync(e);

            ReceiveAsync(receiveArgs);
        }

        private void ReceiveAsync(SocketAsyncEventArgs args)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!args.AcceptSocket!.ReceiveAsync(args))
                    {
                        // ReSharper disable AccessToDisposedClosure
                        Task.Run(() => ReceiveAsyncCompleted(args.AcceptSocket, args));

                        // ReSharper enable AccessToDisposedClosure
                    }
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Aborted);
                    ((ServerClientStateObject)args.UserToken!).Dispose();
                    args.Dispose();
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Error);
                    ((ServerClientStateObject)args.UserToken!).Dispose();
                    args.Dispose();
                }
                catch
                {
                    InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Unspecified);
                    ((ServerClientStateObject)args.UserToken!).Dispose();
                    args.Dispose();
                }
            }
        }

        private void ReceiveAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
                ((ServerClientStateObject)e.UserToken!).Dispose();
                e.Dispose();
                return;
            }
            if (e.BytesTransferred <= 0)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Graceful);
                ((ServerClientStateObject)e.UserToken!).Dispose();
                e.Dispose();
                return;
            }

            Receive(e.AcceptSocket!, e.Buffer!, e.BytesTransferred, (ServerClientStateObject)e.UserToken!);
            ReceiveAsync(e);
        }

        private void SendAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        private protected override unsafe SendError BeginSendTo(Socket        arg0,
                                                                in PacketInfo packetInfo)
        {
            SocketAsyncEventArgs? sendEventArgs = _sendEventArgsPool.Rent();

            if (sendEventArgs == null)
            {
                sendEventArgs           =  new SocketAsyncEventArgs();
                sendEventArgs.Completed += SendAsyncCompleted;
                sendEventArgs.SetBuffer(
                    new byte[_payloadSize + Constants.TCP_HEADER_OFFSET], 0,
                    _payloadSize + Constants.TCP_HEADER_OFFSET);
            }
            sendEventArgs.AcceptSocket = arg0;

            fixed (byte* dst = sendEventArgs.Buffer)
            {
                sendEventArgs.SetBuffer(
                    0,
                    Serialization.Serialization.SerializeTcp(in packetInfo, dst, _encryptionMode));
            }

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

        /// <inheritdoc />
        private protected override void ListenAsync()
        {
#pragma warning disable IDE0068 // Use recommended dispose pattern
            SocketAsyncEventArgs acceptArgs = new SocketAsyncEventArgs();
#pragma warning restore IDE0068 // Use recommended dispose pattern
            acceptArgs.Completed += AcceptAsyncCompleted;
            ListenAsync(acceptArgs);
        }
    }
}