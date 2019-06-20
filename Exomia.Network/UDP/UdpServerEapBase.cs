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
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     A UDP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class UdpServerEapBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        /// <summary>
        ///     _maxPacketSize.
        /// </summary>
        protected readonly int _maxPacketSize;

        /// <summary>
        ///     The receive event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _receiveEventArgsPool;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        protected UdpServerEapBase(uint maxClients, int maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.UDP_PACKET_SIZE_MAX;
            _receiveEventArgsPool = new SocketAsyncEventArgsPool(maxClients + 5);
            _sendEventArgsPool    = new SocketAsyncEventArgsPool(maxClients + 5);
        }

        /// <inheritdoc />
        public override SendError SendTo(EndPoint arg0, uint commandid, byte[] data, int offset, int length,
                                         uint     responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();
                if (sendEventArgs == null)
                {
                    sendEventArgs           =  new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendToAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }
                Serialization.Serialization.SerializeUdp(
                    commandid, data, offset, length, responseid, EncryptionMode.None,
                    sendEventArgs.Buffer,
                    out int size);
                sendEventArgs.SetBuffer(0, size);
                sendEventArgs.RemoteEndPoint = arg0;

                try
                {
                    if (!_listener.SendToAsync(sendEventArgs))
                    {
                        SendToAsyncCompleted(arg0, sendEventArgs);
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

        /// <summary>
        ///     Executes the run action.
        /// </summary>
        /// <param name="port">     The port. </param>
        /// <param name="listener"> [out] The listener. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        private protected override bool OnRun(int port, out Socket listener)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false, DualMode = true
                    };
                    listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false
                    };
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch
            {
                listener = null;
                return false;
            }
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
                    receiveEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }

                receiveEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                try
                {
                    if (!_listener.ReceiveFromAsync(receiveEventArgs))
                    {
                        ReceiveFromAsyncCompleted(receiveEventArgs.AcceptSocket, receiveEventArgs);
                    }
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(receiveEventArgs.RemoteEndPoint, DisconnectReason.Aborted);
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(receiveEventArgs.RemoteEndPoint, DisconnectReason.Error);
                }
                catch { InvokeClientDisconnect(receiveEventArgs.RemoteEndPoint, DisconnectReason.Unspecified); }
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

        /// <summary>
        ///     Receive from asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private unsafe void ReceiveFromAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
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

            ListenAsync();

            e.Buffer.GetHeaderUdp(out byte packetHeader, out uint commandID, out int dataLength);

            if (e.BytesTransferred == dataLength + Constants.UDP_HEADER_SIZE)
            {
                EndPoint ep = e.RemoteEndPoint;

                uint responseID = 0;
                int  offset     = 0;
                fixed (byte* src = e.Buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset     = 4;
                    }
                    byte[] payload;
                    switch ((CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK))
                    {
                        case CompressionMode.Lz4:
                            int l = *(int*)(src + offset);
                            offset += 4;

                            payload = ByteArrayPool.Rent(l);
                            int s = LZ4Codec.Decode(
                                e.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l, true);
                            if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                            DeserializeData(ep, commandID, payload, 0, l, responseID);
                            break;
                        case CompressionMode.None:
                        default:
                            dataLength -= offset;
                            payload    =  ByteArrayPool.Rent(dataLength);

                            fixed (byte* dest = payload)
                            {
                                Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }

                            DeserializeData(ep, commandID, payload, 0, dataLength, responseID);
                            break;
                    }
                }
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