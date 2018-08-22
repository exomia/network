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
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using LZ4;

namespace Exomia.Network.TCP
{
    /// <inheritdoc cref="ClientBase" />
    /// <summary>
    ///     A TCP/UDP-Client build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    public sealed class TcpClientEap : ClientBase
    {
        private const byte ZERO_BYTE = 0;

        private readonly CircularBuffer _circularBuffer;
        private readonly byte[] _buffer;

        /// <inheritdoc />
        public TcpClientEap(ushort maxPacketSize = 0)
        {
            _buffer = new byte[maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX];
            _circularBuffer = new CircularBuffer(_buffer.Length * 2);
        }

        /// <inheritdoc />
        protected override bool TryCreateSocket(out Socket socket)
        {
            try
            {
                if (Socket.OSSupportsIPv6)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false, DualMode = true
                    };
                }
                else
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false
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

        /// <inheritdoc />
        protected override void ReceiveAsync()
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    _clientSocket.BeginReceive(
                        _buffer, 0, _buffer.Length, SocketFlags.None, ReceiveAsyncCallback, null);
                }
                catch (ObjectDisposedException) { Disconnect(DisconnectReason.Aborted); }
                catch (SocketException) { Disconnect(DisconnectReason.Error); }
                catch { Disconnect(DisconnectReason.Unspecified); }
            }
        }

        /// <inheritdoc />
        protected override SendError BeginSendData(uint commandid, byte[] data, int offset, int length,
            uint responseID)
        {
            if (_clientSocket == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                Serialization.Serialization.SerializeTcp(
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

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            _circularBuffer.Dispose();
        }

        private void ReceiveAsyncCallback(IAsyncResult iar)
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

            _circularBuffer.Write(_buffer, 0, length);

            if (!HandleNextPacket())
            {
                ReceiveAsync();
            }
        }

        private bool HandleNextPacket()
        {
            while (true)
            {
                if (_circularBuffer.PeekHeader(
                        0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                    && dataLength <= _circularBuffer.Count - Constants.TCP_HEADER_SIZE)
                {
                    if (_circularBuffer.PeekByte(Constants.TCP_HEADER_SIZE + dataLength) == ZERO_BYTE)
                    {
                        _circularBuffer.Read(_buffer, 0, dataLength, Constants.TCP_HEADER_SIZE);

                        //TODO: deserialize & checksum compare
                        HandleReceive(_buffer, commandID, dataLength, packetHeader);
                        return true;
                    }

                    if (_circularBuffer.SkipUntil(ZERO_BYTE)) { continue; }
                }
                return false;
            }
        }

        private unsafe void HandleReceive(byte[] buffer, uint commandID, int dataLength, byte packetHeader)
        {
            uint responseID = 0;
            byte[] data;
            if ((packetHeader & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
            {
                int l;
                if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                {
                    fixed (byte* ptr = buffer)
                    {
                        responseID = *(uint*)(ptr + Constants.UDP_HEADER_SIZE);
                        l = *(int*)(ptr + Constants.UDP_HEADER_SIZE + 4);
                    }
                    data = ByteArrayPool.Rent(l);
                    int s = LZ4Codec.Decode(
                        buffer, Constants.UDP_HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                }
                else
                {
                    fixed (byte* ptr = buffer)
                    {
                        l = *(int*)(ptr + Constants.UDP_HEADER_SIZE);
                    }
                    data = ByteArrayPool.Rent(l);
                    int s = LZ4Codec.Decode(
                        buffer, Constants.UDP_HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                }
                ReceiveAsync();
                DeserializeData(commandID, data, 0, l, responseID);
            }
            else
            {
                if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                {
                    fixed (byte* ptr = buffer)
                    {
                        responseID = *(uint*)(ptr + Constants.UDP_HEADER_SIZE);
                    }
                    dataLength -= 4;
                    data = ByteArrayPool.Rent(dataLength);
                    Buffer.BlockCopy(buffer, Constants.UDP_HEADER_SIZE + 4, data, 0, dataLength);
                }
                else
                {
                    data = ByteArrayPool.Rent(dataLength);
                    Buffer.BlockCopy(buffer, Constants.UDP_HEADER_SIZE, data, 0, dataLength);
                }
                ReceiveAsync();
                DeserializeData(commandID, data, 0, dataLength, responseID);
            }
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
    }
}