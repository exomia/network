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
    ///     A TCP/UDP-Client build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    public sealed class TcpClientApm : TcpClientBase
    {
        /// <summary>
        ///     Buffer for circular data.
        /// </summary>
        private readonly CircularBuffer _circularBuffer;

        /// <summary>
        ///     The buffer write.
        /// </summary>
        private readonly byte[] _bufferWrite;

        /// <summary>
        ///     The buffer read.
        /// </summary>
        private readonly byte[] _bufferRead;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpClientApm" /> class.
        /// </summary>
        /// <param name="maxPacketSize"> (Optional) Size of the maximum packet. </param>
        public TcpClientApm(ushort maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
            : base(maxPacketSize)
        {
            _bufferWrite = new byte[maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX];
            _bufferRead     = new byte[_bufferWrite.Length];
            _circularBuffer = new CircularBuffer(_bufferWrite.Length * 2);
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
                    _clientSocket.BeginReceive(
                        _bufferWrite, 0, _bufferWrite.Length, SocketFlags.None, ReceiveAsyncCallback, null);
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
            int    size;
            byte[] buffer = ByteArrayPool.Rent(Constants.TCP_HEADER_OFFSET + length + 1);
            fixed (byte* dst = buffer)
            {
                size = Serialization.Serialization.SerializeTcp(
                    packetID, commandID, responseID,
                    src, dst, chunkLength, chunkOffset, length,
                    _encryptionMode, _compressionMode);
            }

            try
            {
                _clientSocket.BeginSend(
                    buffer, 0, size, SocketFlags.None, SendDataCallback, buffer);
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                ByteArrayPool.Return(buffer);
                Disconnect(DisconnectReason.Aborted);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                ByteArrayPool.Return(buffer);
                Disconnect(DisconnectReason.Error);
                return SendError.Socket;
            }
            catch
            {
                ByteArrayPool.Return(buffer);
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
        ///     Async callback, called on completion of receive Asynchronous callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        private void ReceiveAsyncCallback(IAsyncResult iar)
        {
            int bytesTransferred;
            try
            {
                if ((bytesTransferred = _clientSocket.EndReceive(iar)) <= 0)
                {
                    Disconnect(DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                Disconnect(DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                Disconnect(DisconnectReason.Error);
                return;
            }
            catch
            {
                Disconnect(DisconnectReason.Unspecified);
                return;
            }

            if (Serialization.Serialization.DeserializeTcp(
                _circularBuffer, _bufferWrite, _bufferRead, bytesTransferred, _bigDataHandler,
                out uint commandID, out uint responseID, out byte[] data, out int dataLength))
            {
                ReceiveAsync();
                DeserializeData(commandID, data, 0, dataLength, responseID);
                return;
            }

            ReceiveAsync();
        }

        /// <summary>
        ///     Async callback, called on completion of send data callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        private void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                if (_clientSocket.EndSend(iar) <= 0)
                {
                    Disconnect(DisconnectReason.Error);
                }
            }
            catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
            catch (SocketException) { Disconnect(DisconnectReason.Error); }
            catch { Disconnect(DisconnectReason.Unspecified); }

            byte[] send = (byte[])iar.AsyncState;
            ByteArrayPool.Return(send);
        }
    }
}