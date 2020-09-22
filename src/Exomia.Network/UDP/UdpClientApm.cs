#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net.Sockets;
using Exomia.Network.Buffers;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Client build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    public sealed class UdpClientApm : UdpClientBase
    {
        private readonly ObjectPool<ClientStateObject> _clientStateObjectPool;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UdpClientApm" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        public UdpClientApm(ushort expectedMaxPayloadSize = Constants.UDP_PAYLOAD_SIZE_MAX)
            : base(expectedMaxPayloadSize)
        {
            _clientStateObjectPool = new ObjectPool<ClientStateObject>();
        }

        private void ReceiveAsyncCallback(IAsyncResult iar)
        {
            int bytesTransferred;
            try
            {
                if ((bytesTransferred = _clientSocket!.EndReceive(iar)) <= 0)
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

            ReceiveAsync();

            ClientStateObject state = (ClientStateObject)iar.AsyncState!;
            if (Serialization.Serialization.DeserializeUdp(
                state.Buffer, bytesTransferred, _bigDataHandler, i => i,
                out DeserializePacketInfo deserializePacketInfo))
            {
                DeserializeData(in deserializePacketInfo);
            }
            _clientStateObjectPool.Return(state);
        }

        private void SendDataCallback(IAsyncResult iar)
        {
            try
            {
                if (_clientSocket!.EndSend(iar) <= 0)
                {
                    Disconnect(DisconnectReason.Error);
                }
            }
            catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
            catch (SocketException) { Disconnect(DisconnectReason.Error); }
            catch { Disconnect(DisconnectReason.Unspecified); }

            byte[] send = (byte[])iar.AsyncState!;
            ByteArrayPool.Return(send);
        }

        /// <inheritdoc />
        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                ClientStateObject state = _clientStateObjectPool.Rent() ??
                                          new ClientStateObject(new byte[MaxPayloadSize + Constants.UDP_HEADER_OFFSET]);
                try
                {
                    _clientSocket!.BeginReceive(
                        state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveAsyncCallback, state);
                }
                catch (ObjectDisposedException)
                {
                    Disconnect(DisconnectReason.Aborted);
                    _clientStateObjectPool.Return(state);
                }
                catch (SocketException)
                {
                    Disconnect(DisconnectReason.Error);
                    _clientStateObjectPool.Return(state);
                }
                catch
                {
                    Disconnect(DisconnectReason.Unspecified);
                    _clientStateObjectPool.Return(state);
                }
            }
        }

        /// <inheritdoc />
        private protected override unsafe SendError BeginSend(in PacketInfo packetInfo)
        {
            int    size;
            byte[] buffer = ByteArrayPool.Rent(Constants.UDP_HEADER_OFFSET + packetInfo.ChunkLength);
            fixed (byte* dst = buffer)
            {
                size = Serialization.Serialization.SerializeUdp(in packetInfo, dst, _encryptionMode);
            }

            try
            {
                _clientSocket!.BeginSend(
                    buffer, 0, size, SocketFlags.None, SendDataCallback, buffer);
                return SendError.None;
            }
            catch (ObjectDisposedException)
            {
                Disconnect(DisconnectReason.Aborted);
                ByteArrayPool.Return(buffer);
                return SendError.Disposed;
            }
            catch (SocketException)
            {
                Disconnect(DisconnectReason.Error);
                ByteArrayPool.Return(buffer);
                return SendError.Socket;
            }
            catch
            {
                Disconnect(DisconnectReason.Unspecified);
                ByteArrayPool.Return(buffer);
                return SendError.Unknown;
            }
        }

        private sealed class ClientStateObject
        {
            public readonly byte[] Buffer;

            /// <summary>
            ///     Initializes a new instance of the <see cref="UdpClientApm" /> class.
            /// </summary>
            /// <param name="buffer"> The buffer. </param>
            public ClientStateObject(byte[] buffer)
            {
                Buffer = buffer;
            }
        }
    }
}