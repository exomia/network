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
    public sealed class TcpClientApm : ClientBase
    {
        private readonly CircularBuffer _circularBuffer;
        private readonly byte[] _bufferWrite;
        private readonly byte[] _bufferRead;

        /// <inheritdoc />
        public TcpClientApm(ushort maxPacketSize = 0)
        {
            _bufferWrite = new byte[maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX];
            _bufferRead = new byte[_bufferWrite.Length];
            _circularBuffer = new CircularBuffer(_bufferWrite.Length * 2);
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
                        _bufferWrite, 0, _bufferWrite.Length, SocketFlags.None, ReceiveAsyncCallback, null);
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

            int size = _circularBuffer.Write(_bufferWrite, 0, length);
            while (_circularBuffer.PeekHeader(
                       0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                   && dataLength <= _circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (_circularBuffer.PeekByte(Constants.TCP_HEADER_SIZE + dataLength - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    _circularBuffer.Read(_bufferRead, 0, dataLength, Constants.TCP_HEADER_SIZE);
                    if (size < length)
                    {
                        _circularBuffer.Write(_bufferWrite, size, length - size);
                    }

                    //TODO: skip payload bytes then response bit or other things are set in packetHeader
                    byte[] deserializeBuffer = ByteArrayPool.Rent(dataLength);
                    if (Serialization.Serialization.Deserialize(
                            _bufferRead, 0, dataLength - 1, deserializeBuffer, out int bufferLength) == checksum)
                    {
                        HandleReceive(deserializeBuffer, commandID, bufferLength, packetHeader);
                        return;
                    }
                    break;
                }
                bool skipped = _circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += _circularBuffer.Write(_bufferWrite, size, length - size);
                }
                if (!skipped && !_circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            ReceiveAsync();
        }

        private unsafe void HandleReceive(byte[] buffer, uint commandID, int dataLength, byte packetHeader)
        {
            uint responseID = 0;
            if ((packetHeader & Serialization.Serialization.COMPRESSED_BIT_MASK) != 0)
            {
                int l;
                int offset = 4;
                if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                {
                    fixed (byte* ptr = buffer)
                    {
                        responseID = *(uint*)ptr;
                        l = *(int*)(ptr + 4);
                    }
                    offset = 8;
                }
                else
                {
                    fixed (byte* ptr = buffer)
                    {
                        l = *(int*)ptr;
                    }
                }
                byte[] data = ByteArrayPool.Rent(l);
                int s = LZ4Codec.Decode(
                    buffer, offset, dataLength - offset, data, 0, l, true);
                if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                ByteArrayPool.Return(buffer);
                ReceiveAsync();
                DeserializeData(commandID, data, 0, l, responseID);
            }
            else
            {
                int offset = 0;
                if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                {
                    fixed (byte* ptr = buffer)
                    {
                        responseID = *(uint*)ptr;
                    }
                    offset = 4;
                }
                ReceiveAsync();
                DeserializeData(commandID, buffer, offset, dataLength - offset, responseID);
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