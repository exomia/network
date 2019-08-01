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
    ///     A TCP-Client build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    public sealed class TcpClientEap : TcpClientBase
    {
        /// <summary>
        ///     Buffer for circular data.
        /// </summary>
        private readonly CircularBuffer _circularBuffer;

        /// <summary>
        ///     The buffer read.
        /// </summary>
        private readonly byte[] _bufferRead;

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
        /// <param name="maxPacketSize"> (Optional) Size of the maximum packet. </param>
        public TcpClientEap(ushort maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
            : base(maxPacketSize)
        {
            _bufferRead     = new byte[maxPacketSize];
            _circularBuffer = new CircularBuffer(maxPacketSize * 2);

            _receiveEventArgs           =  new SocketAsyncEventArgs();
            _receiveEventArgs.Completed += ReceiveAsyncCompleted;
            _receiveEventArgs.SetBuffer(new byte[maxPacketSize], 0, maxPacketSize);

            _sendEventArgsPool = new SocketAsyncEventArgsPool();
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
                    if (!_clientSocket.ReceiveAsync(_receiveEventArgs))
                    {
                        ReceiveAsyncCompleted(_receiveEventArgs.AcceptSocket, _receiveEventArgs);
                    }
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        private protected override unsafe SendError BeginSendData(int   packetID,
                                                                  uint  commandID,
                                                                  uint  responseID,
                                                                  byte* src,
                                                                  int   chunkLength,
                                                                  int   chunkOffset,
                                                                  int   length)
        {
            SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();
            if (sendEventArgs == null)
            {
                sendEventArgs           =  new SocketAsyncEventArgs();
                sendEventArgs.Completed += SendAsyncCompleted;
                sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
            }

            fixed (byte* dst = sendEventArgs.Buffer)
            {
                sendEventArgs.SetBuffer(
                    0,
                    Serialization.Serialization.SerializeTcp(
                        packetID, commandID, responseID,
                        src, dst, chunkLength, chunkOffset, length,
                        _encryptionMode, _compressionMode));
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
        private void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Disconnect(DisconnectReason.Error);
                return;
            }

            int bytesTransferred = e.BytesTransferred;
            if (bytesTransferred <= 0)
            {
                Disconnect(DisconnectReason.Graceful);
                return;
            }

            if (Serialization.Serialization.DeserializeTcp(
                _circularBuffer, e.Buffer, _bufferRead, bytesTransferred, _bigDataHandler,
                out uint commandID, out uint responseID, out byte[] data, out int dataLength))
            {
                ReceiveAsync();
                DeserializeData(commandID, data, 0, dataLength, responseID);
                return;
            }
            ReceiveAsync();
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