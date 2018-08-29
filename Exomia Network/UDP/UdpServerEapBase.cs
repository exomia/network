#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System;
using System.Net;
using System.Net.Sockets;
using Exomia.Native;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network.UDP
{
    /// <inheritdoc />
    /// <summary>
    ///     A UDP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient">TServerClient</typeparam>
    public abstract class UdpServerEapBase<TServerClient> : ServerBase<EndPoint, TServerClient>
        where TServerClient : ServerClientBase<EndPoint>
    {
        /// <summary>
        ///     _maxPacketSize
        /// </summary>
        protected readonly int _maxPacketSize;

        private readonly SocketAsyncEventArgsPool _receiveEventArgsPool;
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        protected UdpServerEapBase(uint maxClients, int maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.UDP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.UDP_PACKET_SIZE_MAX;
            _receiveEventArgsPool = new SocketAsyncEventArgsPool(maxClients + 5);
            _sendEventArgsPool = new SocketAsyncEventArgsPool(maxClients + 5);
        }

        /// <inheritdoc />
        public override SendError SendTo(EndPoint arg0, uint commandid, byte[] data, int offset, int length,
            uint responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();
                if (sendEventArgs == null)
                {
                    sendEventArgs = new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendToAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }
                Serialization.Serialization.SerializeUdp(
                    commandid, data, offset, length, responseid, EncryptionMode.None, sendEventArgs.Buffer,
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

        /// <inheritdoc />
        protected override bool OnRun(int port, out Socket listener)
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

        /// <inheritdoc />
        protected override void ListenAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveEventArgs = _receiveEventArgsPool.Rent();
                if (receiveEventArgs == null)
                {
                    receiveEventArgs = new SocketAsyncEventArgs();
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
                int offset = 0;
                fixed (byte* src = e.Buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset = 4;
                    }
                    byte[] payload;
                    if ((packetHeader & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                    {
                        int l = *(int*)(src + offset);
                        offset += 4;

                        payload = ByteArrayPool.Rent(l);
                        int s = LZ4Codec.Decode(
                            e.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                        DeserializeData(ep, commandID, payload, 0, l, responseID);
                    }
                    else
                    {
                        dataLength -= offset;
                        payload = ByteArrayPool.Rent(dataLength);

                        fixed (byte* dest = payload)
                        {
                            Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                        }

                        DeserializeData(ep, commandID, payload, 0, dataLength, responseID);
                    }
                }
            }

            _receiveEventArgsPool.Return(e);
        }

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