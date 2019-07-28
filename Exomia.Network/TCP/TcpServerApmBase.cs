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
using Exomia.Network.Encoding;
using Exomia.Network.Native;
using K4os.Compression.LZ4;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Server build with the "Asynchronous Programming Model" (APM)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class TcpServerApmBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     _maxPacketSize.
        /// </summary>
        protected readonly int _maxPacketSize;

        /// <inheritdoc />
        protected TcpServerApmBase(int maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;
        }

        private protected override unsafe SendError SendTo(Socket arg0,
                                                           uint   commandID,
                                                           byte[] data,
                                                           int    offset,
                                                           int    length,
                                                           uint   responseID)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                byte[] send;
                int    size;
                fixed (byte* src = data)
                {
                    Serialization.Serialization.SerializeTcp(
                        commandID, src + offset, length, responseID, EncryptionMode.None,
                        CompressionMode.Lz4, out send, out size);
                }

                try
                {
                    SendStateObject state;
                    state.Socket = arg0;
                    state.Buffer = send;
                    arg0.BeginSend(send, 0, size, SocketFlags.None, BeginSendCallback, state);

                    return SendError.None;
                }
                catch (ObjectDisposedException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Aborted);
                    ByteArrayPool.Return(send);
                    return SendError.Disposed;
                }
                catch (SocketException)
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Error);
                    ByteArrayPool.Return(send);
                    return SendError.Socket;
                }
                catch
                {
                    InvokeClientDisconnect(arg0, DisconnectReason.Unspecified);
                    ByteArrayPool.Return(send);
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
                    listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false, DualMode = true
                    };
                    listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true, Blocking = false
                    };
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                }

                listener.Listen(100);
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
            try
            {
                _listener.BeginAccept(AcceptCallback, null);
            }
            catch (ObjectDisposedException)
            {
                /* SOCKET CLOSED */
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <inheritdoc />
        protected override void OnAfterClientDisconnect(Socket arg0)
        {
            try
            {
                arg0?.Shutdown(SocketShutdown.Both);
                arg0?.Close(CLOSE_TIMEOUT);
            }
            catch
            {
                /* IGNORE */
            }
        }

        /// <summary>
        ///     Async callback, called on completion of begin send callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        private void BeginSendCallback(IAsyncResult iar)
        {
            SendStateObject state = (SendStateObject)iar.AsyncState;
            try
            {
                if (state.Socket.EndSend(iar) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                }
            }
            catch
            {
                /* IGNORE */
            }
            finally
            {
                ByteArrayPool.Return(state.Buffer);
            }
        }

        /// <summary>
        ///     Async callback, called on completion of accept callback.
        /// </summary>
        /// <param name="ar"> The archive. </param>
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = _listener.EndAccept(ar);
                ServerClientStateObject state = new ServerClientStateObject
                {
                    //0.2mb
                    Socket         = socket,
                    BufferWrite    = new byte[_maxPacketSize],
                    BufferRead     = new byte[_maxPacketSize],
                    CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ReceiveAsync(state);
            }
            catch (ObjectDisposedException)
            {
                /* SOCKET CLOSED */
                return;
            }
            catch
            {
                /* IGNORE */
            }

            ListenAsync();
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        /// <param name="state"> The state. </param>
        private void ReceiveAsync(ServerClientStateObject state)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    state.Socket.BeginReceive(
                        state.BufferWrite, 0, state.BufferWrite.Length, SocketFlags.None, ReceiveDataCallback,
                        state);
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(state.Socket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified); }
            }
        }

        /// <summary>
        ///     Async callback, called on completion of receive data callback.
        /// </summary>
        /// <param name="iar"> The iar. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private unsafe void ReceiveDataCallback(IAsyncResult iar)
        {
            ServerClientStateObject state = (ServerClientStateObject)iar.AsyncState;
            int                     length;
            try
            {
                if ((length = state.Socket.EndReceive(iar)) <= 0)
                {
                    InvokeClientDisconnect(state.Socket, DisconnectReason.Graceful);
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Aborted);
                return;
            }
            catch (SocketException)
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Error);
                return;
            }
            catch
            {
                InvokeClientDisconnect(state.Socket, DisconnectReason.Unspecified);
                return;
            }

            CircularBuffer circularBuffer = state.CircularBuffer;
            int            size           = circularBuffer.Write(state.BufferWrite, 0, length);
            while (circularBuffer.PeekHeader(
                       0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum)
                && dataLength <= circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (circularBuffer.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = state.BufferRead)
                    {
                        circularBuffer.Read(ptr, 0, dataLength, Constants.TCP_HEADER_SIZE);
                        if (size < length)
                        {
                            circularBuffer.Write(state.BufferWrite, size, length - size);
                        }

                        uint responseID = 0;
                        int  offset     = 0;
                        if ((packetHeader & Serialization.Serialization.RESPONSE_BIT_MASK) != 0)
                        {
                            responseID = *(uint*)ptr;
                            offset     = 4;
                        }

                        CompressionMode compressionMode =
                            (CompressionMode)(packetHeader & Serialization.Serialization.COMPRESSED_MODE_MASK);
                        if (compressionMode != CompressionMode.None)
                        {
                            offset += 4;
                        }

                        byte[] deserializeBuffer = ByteArrayPool.Rent(dataLength);
                        if (PayloadEncoding.Decode(
                                ptr, offset, dataLength - 1, deserializeBuffer, out int bufferLength) == checksum)
                        {
                            switch (compressionMode)
                            {
                                case CompressionMode.None:
                                    break;
                                case CompressionMode.Lz4:
                                    int l = *(int*)(ptr + offset);

                                    byte[] buffer = ByteArrayPool.Rent(l);
                                    int    s      = LZ4Codec.Decode(deserializeBuffer, 0, bufferLength, buffer, 0, l);
                                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                    ByteArrayPool.Return(deserializeBuffer);
                                    deserializeBuffer = buffer;
                                    bufferLength      = l;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(CompressionMode),
                                        (CompressionMode)(packetHeader &
                                                          Serialization.Serialization.COMPRESSED_MODE_MASK),
                                        "Not supported!");
                            }

                            ReceiveAsync(state);
                            DeserializeData(state.Socket, commandID, deserializeBuffer, 0, bufferLength, responseID);
                            return;
                        }
                        break;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += circularBuffer.Write(state.BufferWrite, size, length - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            ReceiveAsync(state);
        }

        /// <summary>
        ///     A send state object.
        /// </summary>
        private struct SendStateObject
        {
            /// <summary>
            ///     The buffer.
            /// </summary>
            public byte[] Buffer;

            /// <summary>
            ///     The socket.
            /// </summary>
            public Socket Socket;
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer write.
            /// </summary>
            public byte[] BufferWrite;

            /// <summary>
            ///     The buffer read.
            /// </summary>
            public byte[] BufferRead;

            /// <summary>
            ///     Buffer for circular data.
            /// </summary>
            public CircularBuffer CircularBuffer;

            /// <summary>
            ///     The socket.
            /// </summary>
            public Socket Socket;
        }
    }
}