﻿#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class UdpClientEap : UdpClientBase
    {
        /// <summary>
        ///     The receive event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _receiveEventArgsPool;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpClientEap" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> (Optional) Size of the maximum packet. </param>
        public UdpClientEap(ushort maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
            : base(maxPacketSize)
        {
            _receiveEventArgsPool = new SocketAsyncEventArgsPool();
            _sendEventArgsPool    = new SocketAsyncEventArgsPool();
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveEventArgs = _receiveEventArgsPool.Rent();
                if (receiveEventArgs == null)
                {
                    receiveEventArgs           =  new SocketAsyncEventArgs();
                    receiveEventArgs.Completed += ReceiveAsyncCompleted;
                    receiveEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }
                try
                {
                    if (!_clientSocket.ReceiveAsync(receiveEventArgs))
                    {
                        ReceiveAsyncCompleted(receiveEventArgs.AcceptSocket, receiveEventArgs);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Disconnect(DisconnectReason.Aborted);
                    _receiveEventArgsPool.Return(receiveEventArgs);
                }
                catch (SocketException)
                {
                    Disconnect(DisconnectReason.Error);
                    _receiveEventArgsPool.Return(receiveEventArgs);
                }
                catch
                {
                    Disconnect(DisconnectReason.Unspecified);
                    _receiveEventArgsPool.Return(receiveEventArgs);
                }
            }
        }

        private protected override unsafe SendError BeginSendData(uint   commandID,
                                                                  byte[] data,
                                                                  int    offset,
                                                                  int    length,
                                                                  uint   responseID)
        {
            if (_clientSocket == null) { return SendError.Invalid; }
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
                    Serialization.Serialization.SerializeUdp(
                        commandID, src + offset, length, responseID, EncryptionMode.None,
                        CompressionMode.Lz4, dst, out int size);
                    sendEventArgs.SetBuffer(0, size);
                }

                try
                {
                    if (!_clientSocket.SendAsync(sendEventArgs))
                    {
                        SendAsyncCompleted(_clientSocket, sendEventArgs);
                    }
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    Disconnect(DisconnectReason.Aborted);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    Disconnect(DisconnectReason.Error);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Socket;
                }
                catch
                {
                    Disconnect(DisconnectReason.Unspecified);
                    _sendEventArgsPool.Return(sendEventArgs);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
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
                Disconnect(DisconnectReason.Error);
                return;
            }
            if (e.BytesTransferred <= 0)
            {
                Disconnect(DisconnectReason.Graceful);
                return;
            }

            Receive(e.Buffer, e.BytesTransferred);
            _receiveEventArgsPool.Return(e);
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
                Disconnect(DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }
    }
}