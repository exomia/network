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
using LZ4;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP-Server build with the "Event-based Asynchronous Pattern" (EAP)
    /// </summary>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    public abstract class TcpServerEapBase<TServerClient> : ServerBase<Socket, TServerClient>
        where TServerClient : ServerClientBase<Socket>
    {
        /// <summary>
        ///     _maxPacketSize.
        /// </summary>
        protected readonly int _maxPacketSize;

        /// <summary>
        ///     The send event arguments pool.
        /// </summary>
        private readonly SocketAsyncEventArgsPool _sendEventArgsPool;

        /// <inheritdoc />
        protected TcpServerEapBase(uint expectedMaxClient = 32, int maxPacketSize = Constants.TCP_PACKET_SIZE_MAX)
        {
            _maxPacketSize = maxPacketSize > 0 && maxPacketSize < Constants.TCP_PACKET_SIZE_MAX
                ? maxPacketSize
                : Constants.TCP_PACKET_SIZE_MAX;

            _sendEventArgsPool = new SocketAsyncEventArgsPool(expectedMaxClient);
        }

        /// <inheritdoc />
        public override SendError SendTo(Socket arg0, uint commandid, byte[] data, int offset, int length,
                                         uint   responseid)
        {
            if (_listener == null) { return SendError.Invalid; }
            if ((_state & SEND_FLAG) == SEND_FLAG)
            {
                SocketAsyncEventArgs sendEventArgs = _sendEventArgsPool.Rent();

                if (sendEventArgs == null)
                {
                    sendEventArgs           =  new SocketAsyncEventArgs();
                    sendEventArgs.Completed += SendAsyncCompleted;
                    sendEventArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                }

                Serialization.Serialization.SerializeTcp(
                    commandid, data, offset, length, responseid, EncryptionMode.None,
                    sendEventArgs.Buffer,
                    out int size);
                sendEventArgs.SetBuffer(0, size);
                sendEventArgs.AcceptSocket = arg0;

                try
                {
                    if (!arg0.SendAsync(sendEventArgs))
                    {
                        SendAsyncCompleted(arg0, sendEventArgs);
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

        /// <inheritdoc />
        private protected override void ListenAsync()
        {
            SocketAsyncEventArgs acceptArgs = new SocketAsyncEventArgs();
            acceptArgs.Completed += AcceptAsyncCompleted;
            ListenAsync(acceptArgs);
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
        ///     Listen asynchronous.
        /// </summary>
        /// <param name="acceptArgs"> Socket asynchronous event information. </param>
        private void ListenAsync(SocketAsyncEventArgs acceptArgs)
        {
            acceptArgs.AcceptSocket = null;
            try
            {
                if (!_listener.AcceptAsync(acceptArgs))
                {
                    AcceptAsyncCompleted(_listener, acceptArgs);
                }
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

        /// <summary>
        ///     Accept asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        private void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                try
                {
                    e.AcceptSocket?.Shutdown(SocketShutdown.Both);
                    e.AcceptSocket?.Close(CLOSE_TIMEOUT);
                }
                catch
                {
                    /* IGNORE */
                }
                ListenAsync(e);
                return;
            }
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs { AcceptSocket = e.AcceptSocket };
                receiveArgs.Completed += ReceiveAsyncCompleted;
                receiveArgs.SetBuffer(new byte[_maxPacketSize], 0, _maxPacketSize);
                receiveArgs.UserToken = new ServerClientStateObject
                {
                    BufferRead = new byte[_maxPacketSize], CircularBuffer = new CircularBuffer(_maxPacketSize * 2)
                };

                ListenAsync(e);

                ReceiveAsync(receiveArgs);
            }
        }

        /// <summary>
        ///     Receive asynchronous.
        /// </summary>
        /// <param name="args"> Socket asynchronous event information. </param>
        private void ReceiveAsync(SocketAsyncEventArgs args)
        {
            if ((_state & RECEIVE_FLAG) == RECEIVE_FLAG)
            {
                try
                {
                    if (!args.AcceptSocket.ReceiveAsync(args))
                    {
                        ReceiveAsyncCompleted(args.AcceptSocket, args);
                    }
                }
                catch (ObjectDisposedException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Aborted); }
                catch (SocketException) { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Error); }
                catch { InvokeClientDisconnect(args.AcceptSocket, DisconnectReason.Unspecified); }
            }
        }

        /// <summary>
        ///     Receive asynchronous completed.
        /// </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Socket asynchronous event information. </param>
        /// <exception cref="Exception"> Thrown when an exception error condition occurs. </exception>
        private unsafe void ReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
                return;
            }
            int length = e.BytesTransferred;
            if (length <= 0)
            {
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Graceful);
                return;
            }

            ServerClientStateObject state          = (ServerClientStateObject)e.UserToken;
            CircularBuffer          circularBuffer = state.CircularBuffer;

            int size = circularBuffer.Write(e.Buffer, 0, length);
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
                            circularBuffer.Write(e.Buffer, size, length - size);
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
                                case CompressionMode.Lz4:
                                    int l = *(int*)(ptr + offset);

                                    byte[] buffer = ByteArrayPool.Rent(l);
                                    int s = LZ4Codec.Decode(
                                        deserializeBuffer, 0, bufferLength, buffer, 0, l, true);
                                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                    ByteArrayPool.Return(deserializeBuffer);
                                    deserializeBuffer = buffer;
                                    bufferLength      = l;
                                    break;
                            }

                            ReceiveAsync(e);
                            DeserializeData(e.AcceptSocket, commandID, deserializeBuffer, 0, bufferLength, responseID);
                            return;
                        }
                        break;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < length)
                {
                    size += circularBuffer.Write(e.Buffer, size, length - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            ReceiveAsync(e);
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
                InvokeClientDisconnect(e.AcceptSocket, DisconnectReason.Error);
            }
            _sendEventArgsPool.Return(e);
        }

        /// <summary>
        ///     A server client state object. This class cannot be inherited.
        /// </summary>
        private sealed class ServerClientStateObject
        {
            /// <summary>
            ///     The buffer read.
            /// </summary>
            public byte[] BufferRead;

            /// <summary>
            ///     Buffer for circular data.
            /// </summary>
            public CircularBuffer CircularBuffer;
        }
    }
}