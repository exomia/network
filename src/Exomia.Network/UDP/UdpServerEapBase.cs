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

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public abstract class UdpServerEapBase<TServerClient> : UdpServerBase<TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
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
        ///     Initializes a new instance of the <see cref="UdpServerEapBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxClients">     The expected maximum clients. </param>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        protected UdpServerEapBase(ushort expectedMaxClients,
                                   ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _receiveEventArgsPool = new SocketAsyncEventArgsPool((ushort)(expectedMaxClients + 5));
            _sendEventArgsPool    = new SocketAsyncEventArgsPool((ushort)(expectedMaxClients + 5));
        }

        /// <summary>
        ///     Listen asynchronous.
        /// </summary>
        private protected override void ListenAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveEventArgs = _receiveEventArgsPool.Rent();
                if (receiveEventArgs == null)
                {
                    receiveEventArgs           =  new SocketAsyncEventArgs();
                    receiveEventArgs.Completed += ReceiveFromAsyncCompleted;
                    receiveEventArgs.SetBuffer(
                        new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET],
                        0, MaxPayloadSize + Constants.UDP_HEADER_OFFSET);
                }

                receiveEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                try
                {
                    if (!_listener.ReceiveFromAsync(receiveEventArgs))
                    {
                        ReceiveFromAsyncCompleted(receiveEventArgs.RemoteEndPoint, receiveEventArgs);
                    }
                }
                catch
                {
                    _receiveEventArgsPool.Return(receiveEventArgs);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _receiveEventArgsPool?.Dispose();
                _sendEventArgsPool?.Dispose();
            }
        }

        /// <inheritdoc />
        private protected override unsafe SendError SendTo(EndPoint      arg0,
                                                           in PacketInfo packetInfo)
        {
            SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();
            if (sendEventArgs == null)
            {
                sendEventArgs           =  new SocketAsyncEventArgs();
                sendEventArgs.Completed += SendToAsyncCompleted;
                sendEventArgs.SetBuffer(
                    new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET],
                    0, MaxPayloadSize + Constants.UDP_HEADER_OFFSET);
            }
            sendEventArgs.RemoteEndPoint = arg0;

            fixed (byte* dst = sendEventArgs.Buffer)
            {
                sendEventArgs.SetBuffer(
                    0,
                    Serialization.Serialization.SerializeUdp(in packetInfo, dst, _encryptionMode));
            }

            try
            {
                if (!_listener.SendToAsync(sendEventArgs))
                {
                    SendToAsyncCompleted(sendEventArgs.RemoteEndPoint, sendEventArgs);
                }
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(sendEventArgs.RemoteEndPoint, DisconnectReason.Aborted);
                _sendEventArgsPool.Return(sendEventArgs);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(sendEventArgs.RemoteEndPoint, DisconnectReason.Error);
                _sendEventArgsPool.Return(sendEventArgs);
                return SendError.Socket;
            }
            catch
            {
                InvokeClientDisconnect(sendEventArgs.RemoteEndPoint, DisconnectReason.Unspecified);
                _sendEventArgsPool.Return(sendEventArgs);
                return SendError.Unknown;
            }
        }

        /// <summary>
        ///     Receive from asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private void ReceiveFromAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ListenAsync();

            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.RemoteEndPoint, DisconnectReason.Error);
                return;
            }
            if (e.BytesTransferred <= 0)
            {
                InvokeClientDisconnect(e.RemoteEndPoint, DisconnectReason.Graceful);
                return;
            }

            if (Serialization.Serialization.DeserializeUdp(
                e.Buffer, e.BytesTransferred, _bigDataHandler,
                out DeserializePacketInfo deserializePacketInfo))
            {
                DeserializeData(e.RemoteEndPoint, in deserializePacketInfo);
            }

            _receiveEventArgsPool.Return(e);
        }

        /// <summary>
        ///     Sends to asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void SendToAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.RemoteEndPoint, DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }
    }
}