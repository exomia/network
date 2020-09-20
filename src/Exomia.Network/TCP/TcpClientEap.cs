#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class TcpClientEap : TcpClientBase
    {
        /// <summary>
        ///     Socket asynchronous event information.
        /// </summary>
        private readonly SocketAsyncEventArgs _receiveEventArgs;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpClientEap" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        public TcpClientEap(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _receiveEventArgs           =  new SocketAsyncEventArgs();
            _receiveEventArgs.Completed += ReceiveAsyncCompleted;
            _receiveEventArgs.SetBuffer(new byte[_bufferRead.Length], 0, _bufferRead.Length);

            _sendEventArgsPool = new SocketAsyncEventArgsPool();
        }

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            _circularBuffer.Dispose();
        }

        /// <summary>
        ///     Receive asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void ReceiveAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
                return;
            }

            if (e.BytesTransferred <= 0)
            {
                Disconnect(DisconnectReason.Graceful);
                return;
            }

            Receive(e.Buffer, e.BytesTransferred);
            ReceiveAsync();
        }

        /// <summary>
        ///     Sends an asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void SendAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!_clientSocket!.ReceiveAsync(_receiveEventArgs))
                    {
                        ReceiveAsyncCompleted(_receiveEventArgs.AcceptSocket, _receiveEventArgs);
                    }
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        private protected override unsafe SendError BeginSend(in PacketInfo packetInfo)
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

            fixed (byte* dst = sendEventArgs.Buffer)
            {
                sendEventArgs.SetBuffer(
                    0, Serialization.Serialization.SerializeTcp(in packetInfo, dst, _encryptionMode));
            }

            try
            {
                if (!_clientSocket!.SendAsync(sendEventArgs))
                {
                    SendAsyncCompleted(_clientSocket, sendEventArgs);
                }
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                Disconnect(DisconnectReason.Aborted);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                Disconnect(DisconnectReason.Error);
                return SendError.Socket;
            }
            catch
            {
                Disconnect(DisconnectReason.Unspecified);
                return SendError.Unknown;
            }
        }
    }
}