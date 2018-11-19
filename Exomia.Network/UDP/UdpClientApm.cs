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

#pragma warning disable 1574

using System;
using System.Net.Sockets;
using Exomia.Native;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;
using LZ4;

namespace Exomia.Network.UDP
{
    /// <inheritdoc cref="ClientBase" />
    /// <summary>
    ///     A UDP-Client build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    public sealed class UdpClientApm : ClientBase
    {
        private readonly ClientStateObject _stateObj;

        /// <inheritdoc />
        public UdpClientApm(ushort maxPacketSize = Constants.UDP_PACKET_SIZE_MAX)
        {
            _stateObj = new ClientStateObject(
                new byte[(maxPacketSize > 0) & (maxPacketSize < Constants.UDP_PACKET_SIZE_MAX)
                    ? maxPacketSize
                    : Constants.UDP_PACKET_SIZE_MAX]);
        }

        private protected override bool TryCreateSocket(out Socket socket)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false, DualMode = true
                    };
                }
                else
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        Blocking = false
                    };
                }
                return true;
            }
            catch
            {
                socket = null;
                return false;
            }
        }

        private protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    _clientSocket.BeginReceive(
                        _stateObj.Buffer, 0, _stateObj.Buffer.Length, SocketFlags.None, ReceiveAsyncCallback, null);
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        private protected override SendError BeginSendData(uint commandid, byte[] data, int offset, int length,
            uint responseID)
        {
            if (_clientSocket == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.SerializeUdp(
                    commandid, data, offset, length, responseID, EncryptionMode.None, out byte[] send, out int size);
                try
                {
                    _clientSocket.BeginSend(
                        send, 0, size, SocketFlags.None, SendDataCallback, send);
                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Aborted);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Error);
                    return SendError.Socket;
                }
                catch
                {
                    ByteArrayPool.Return(send);
                    Disconnect(DisconnectReason.Unspecified);
                    return SendError.Unknown;
                }
            }
            return SendError.Invalid;
        }

        private unsafe void ReceiveAsyncCallback(IAsyncResult iar)
        {
            int length;
            try
            {
                if ((length = _clientSocket.EndReceive(iar)) <= 0)
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

            _stateObj.Buffer.GetHeaderUdp(out byte packetHeader, out uint commandID, out int dataLength);

            if (length == dataLength + Constants.UDP_HEADER_SIZE)
            {
                uint responseID = 0;
                int offset = 0;
                fixed (byte* src = _stateObj.Buffer)
                {
                    if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID = *(uint*)src;
                        offset     = 4;
                    }
                    byte[] payload;
                    if ((packetHeader & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
                    {
                        int l = *(int*)(src + offset);
                        offset += 4;

                        payload = ByteArrayPool.Rent(l);
                        int s = LZ4Codec.Decode(
                            _stateObj.Buffer, Constants.UDP_HEADER_SIZE + offset, dataLength - offset, payload, 0, l,
                            true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                        ReceiveAsync();
                        DeserializeData(commandID, payload, 0, l, responseID);
                    }
                    else
                    {
                        dataLength -= offset;
                        payload    =  ByteArrayPool.Rent(dataLength);

                        fixed (byte* dest = payload)
                        {
                            Mem.Cpy(dest, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                        }

                        ReceiveAsync();
                        DeserializeData(commandID, payload, 0, dataLength, responseID);
                    }
                }
                return;
            }

            ReceiveAsync();
        }

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

        private struct ClientStateObject
        {
            public readonly byte[] Buffer;

            public ClientStateObject(byte[] buffer)
            {
                Buffer = buffer;
            }
        }
    }
}