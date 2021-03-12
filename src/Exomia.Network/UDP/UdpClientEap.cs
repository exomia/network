#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class UdpClientEap : UdpClientBase
    {
        private readonly SocketAsyncEventArgsPool _receiveEventArgsPool;
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpClientEap" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        public UdpClientEap(ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _receiveEventArgsPool = new SocketAsyncEventArgsPool();
            _sendEventArgsPool    = new SocketAsyncEventArgsPool();
        }

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

            ReceiveAsync();

            if (Serialization.Serialization.DeserializeUdp(
                e.Buffer, e.BytesTransferred, _bigDataHandler, i => i,
                out DeserializePacketInfo deserializePacketInfo))
            {
                DeserializeData(in deserializePacketInfo);
            }
        }

        private void SendAsyncCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        /// <inheritdoc />
        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs? receiveEventArgs = _receiveEventArgsPool.Rent();
                if (receiveEventArgs == null)
                {
                    receiveEventArgs           =  new SocketAsyncEventArgs();
                    receiveEventArgs.Completed += ReceiveAsyncCompleted;
                    receiveEventArgs.SetBuffer(
                        new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET],
                        0, MaxPayloadSize + Constants.UDP_HEADER_OFFSET);
                }

                try
                {
                    if (!_clientSocket!.ReceiveAsync(receiveEventArgs))
                    {
                        Task.Run(() => ReceiveAsyncCompleted(receiveEventArgs.RemoteEndPoint, receiveEventArgs));
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

        /// <inheritdoc />
        private protected override unsafe SendError BeginSend(in PacketInfo packetInfo)
        {
            SocketAsyncEventArgs? sendEventArgs = _sendEventArgsPool.Rent();
            if (sendEventArgs == null)
            {
                sendEventArgs           =  new SocketAsyncEventArgs();
                sendEventArgs.Completed += SendAsyncCompleted;
                sendEventArgs.SetBuffer(
                    new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET],
                    0, MaxPayloadSize + Constants.UDP_HEADER_OFFSET);
            }

            fixed (byte* dst = sendEventArgs.Buffer)
            {
                sendEventArgs.SetBuffer(
                    0,
                    Serialization.Serialization.SerializeUdp(in packetInfo, dst, _encryptionMode));
            }

            try
            {
                if (!_clientSocket!.SendAsync(sendEventArgs))
                {
                    SendAsyncCompleted(sendEventArgs.RemoteEndPoint, sendEventArgs);
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
    }
}