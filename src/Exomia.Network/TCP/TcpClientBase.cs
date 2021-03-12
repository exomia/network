#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Exomia.Network.Encoding;
using Exomia.Network.Native;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP client base.
    /// </summary>
    public abstract class TcpClientBase : ClientBase
    {
        private readonly           ushort              _maxPayloadSize;
        private protected readonly CircularBuffer      _circularBuffer;
        private protected readonly byte[]              _bufferRead;
        private protected readonly ushort              _payloadSize;
        private protected readonly BigDataHandler<int> _bigDataHandler;

        /// <inheritdoc />
        private protected override ushort MaxPayloadSize
        {
            get { return _maxPayloadSize; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerBase{TServerClient}" /> class.
        /// </summary>
        /// <param name="expectedMaxPayloadSize"> (Optional) Size of the expected maximum payload. </param>
        private protected TcpClientBase(ushort expectedMaxPayloadSize = Constants.TCP_PAYLOAD_SIZE_MAX)
        {
            _maxPayloadSize = expectedMaxPayloadSize > 0 && expectedMaxPayloadSize < Constants.TCP_PAYLOAD_SIZE_MAX
                ? expectedMaxPayloadSize
                : Constants.TCP_PAYLOAD_SIZE_MAX;
            _payloadSize = (ushort)(PayloadEncoding.EncodedPayloadLength(_maxPayloadSize) + 1);

            _bufferRead     = new byte[_payloadSize + Constants.TCP_HEADER_OFFSET];
            _circularBuffer = new CircularBuffer(_bufferRead.Length * 2);
            _bigDataHandler = new BigDataHandler<int>.Default();
        }

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _bigDataHandler.Dispose();
            }
        }

        /// <inheritdoc />
        private protected override void Configure()
        {
            ReceiveBufferSize = 8 * 1024; //8kb
            SendBufferSize    = 8 * 1024; //8kb
        }

#if NETSTANDARD2_1
        /// <inheritdoc />
        private protected override bool TryCreateSocket([NotNullWhen(true)] out Socket? socket)
#else
        /// <inheritdoc/>
        private protected override bool TryCreateSocket(out Socket? socket)
#endif
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

        private protected unsafe void Receive(byte[] buffer,
                                              int    bytesTransferred)
        {
            DeserializePacketInfo deserializePacketInfo;
            int                   size = _circularBuffer.Write(buffer, 0, bytesTransferred);
            while (_circularBuffer.PeekHeader(
                       0, out byte packetHeader, out deserializePacketInfo.CommandOrResponseID,
                       out deserializePacketInfo.Length, out ushort checksum)
                && deserializePacketInfo.Length <= _circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (_circularBuffer.PeekByte(
                        (Constants.TCP_HEADER_SIZE + deserializePacketInfo.Length) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = _bufferRead)
                    {
                        _circularBuffer.Read(ptr, deserializePacketInfo.Length, Constants.TCP_HEADER_SIZE);
                        if (size < bytesTransferred)
                        {
                            size += _circularBuffer.Write(buffer, size, bytesTransferred - size);
                        }
                    }

                    if (Serialization.Serialization.DeserializeTcp(
                        packetHeader, checksum, _bufferRead, _bigDataHandler,
                        out deserializePacketInfo.Data, ref deserializePacketInfo.Length,
                        out deserializePacketInfo.RequestID, out deserializePacketInfo.IsResponseBitSet))
                    {
                        DeserializeData(in deserializePacketInfo);
                    }

                    continue;
                }
                bool skipped = _circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < bytesTransferred)
                {
                    size += _circularBuffer.Write(buffer, size, bytesTransferred - size);
                }
                if (!skipped && !_circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
        }
    }
}