#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Native;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

namespace Exomia.Network
{
    public abstract partial class ClientBase
    {
        /// <inheritdoc />
        public SendError Send(ushort commandOrResponseID, byte[] data, int offset, int length, bool isResponse = false)
        {
            return BeginSend(commandOrResponseID, data, offset, length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError Send(ushort commandOrResponseID, byte[] data, bool isResponse = false)
        {
            return BeginSend(commandOrResponseID, data, 0, data.Length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError Send<T>(ushort commandOrResponseID, in T data, bool isResponse = false) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return BeginSend(commandOrResponseID, dataB, 0, length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError Send(ushort commandOrResponseID, ISerializable serializable, bool isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return BeginSend(commandOrResponseID, dataB, 0, length, 0, isResponse);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                            byte[]                            data,
                                                            int                               offset,
                                                            int                               length,
                                                            DeserializePacketHandler<TResult> deserialize,
                                                            TimeSpan                          timeout,
                                                            bool                              isResponse = false)
        {
            TaskCompletionSource<(ushort requestID, Packet packet)> tcs =
                new TaskCompletionSource<(ushort, Packet)>(TaskCreationOptions.None);
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);
            ushort                        requestID;
            bool                          lockTaken = false;
            try
            {
                _lockTaskCompletionSources.Enter(ref lockTaken);
                requestID = _requestID++;
                if (requestID == 0) { requestID = _requestID++; }
                _taskCompletionSources.Add(requestID, tcs);
            }
            finally
            {
                if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
            }
            cts.Token.Register(
                delegate
                {
                    bool lockTaken1 = false;
                    try
                    {
                        _lockTaskCompletionSources.Enter(ref lockTaken1);
                        _taskCompletionSources.Remove(requestID);
                    }
                    finally
                    {
                        if (lockTaken1) { _lockTaskCompletionSources.Exit(false); }
                    }
                    tcs.TrySetResult(default);
                }, false);
            SendError sendError = BeginSend(commandOrResponseID, data, offset, length, requestID, isResponse);
            if (sendError == SendError.None)
            {
                (ushort rID, Packet packet) = await tcs.Task;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (packet.Buffer != null)
                {
                    if (deserialize(in packet, out TResult result))
                    {
                        ByteArrayPool.Return(packet.Buffer);
                        return new Response<TResult>(result, rID, SendError.None);
                    }

                    ByteArrayPool.Return(packet.Buffer);
                }
                sendError = SendError.Unknown; //TimeOut Error | Deserialization failed
            }
            lockTaken = false;
            try
            {
                _lockTaskCompletionSources.Enter(ref lockTaken);
                _taskCompletionSources.Remove(requestID);
            }
            finally
            {
                if (lockTaken) { _lockTaskCompletionSources.Exit(false); }
            }
            return
                new Response<TResult>(
                    default!, //default!: can be null, but it doesn't matter if an error has occurred, it is unsafe to use it anyway.
                    0,
                    sendError);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort commandOrResponseID, byte[] data, bool isResponse = false)
            where TResult : unmanaged
        {
            return SendR<TResult>(
                commandOrResponseID, data, 0, data.Length, DeserializeResponse, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort commandOrResponseID,
                                                      byte[] data,
                                                      int    offset,
                                                      int    length,
                                                      bool   isResponse = false)
            where TResult : unmanaged
        {
            return SendR<TResult>(
                commandOrResponseID, data, offset, length, DeserializeResponse, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                      byte[]                            data,
                                                      int                               offset,
                                                      int                               length,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      bool                              isResponse = false)
        {
            return SendR(commandOrResponseID, data, offset, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      bool                              isResponse = false)
        {
            return SendR(commandOrResponseID, data, 0, data.Length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort   commandOrResponseID,
                                                      byte[]   data,
                                                      int      offset,
                                                      int      length,
                                                      TimeSpan timeout,
                                                      bool     isResponse = false)
            where TResult : unmanaged
        {
            return SendR<TResult>(commandOrResponseID, data, offset, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort   commandOrResponseID,
                                                      byte[]   data,
                                                      TimeSpan timeout,
                                                      bool     isResponse = false)
            where TResult : unmanaged
        {
            return SendR<TResult>(commandOrResponseID, data, 0, data.Length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout,
                                                      bool                              isResponse = false)
        {
            return SendR(commandOrResponseID, data, 0, data.Length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort        commandOrResponseID,
                                                      ISerializable serializable,
                                                      bool          isResponse = false)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR<TResult>(
                commandOrResponseID, dataB, 0, length, DeserializeResponse, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      bool                              isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandOrResponseID, dataB, 0, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort        commandOrResponseID,
                                                      ISerializable serializable,
                                                      TimeSpan      timeout,
                                                      bool          isResponse = false)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR<TResult>(commandOrResponseID, dataB, 0, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(ushort                            commandOrResponseID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout,
                                                      bool                              isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandOrResponseID, dataB, 0, length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(ushort commandOrResponseID, in T data, bool isResponse = false)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR<TResult>(
                commandOrResponseID, dataB, 0, length, DeserializeResponse, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(ushort                            commandOrResponseID,
                                                         in T                              data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         bool                              isResponse = false)
            where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandOrResponseID, dataB, 0, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(ushort   commandOrResponseID,
                                                         in T     data,
                                                         TimeSpan timeout,
                                                         bool     isResponse = false)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR<TResult>(commandOrResponseID, dataB, 0, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(ushort commandOrResponseID,
                                                         in T data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         TimeSpan timeout,
                                                         bool isResponse = false) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandOrResponseID, dataB, 0, length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public SendError SendPing()
        {
            return Send(CommandID.PING, new PingPacket(DateTime.Now.Ticks));
        }

        /// <inheritdoc />
        public Task<Response<PingPacket>> SendRPing()
        {
            return SendR<PingPacket, PingPacket>(
                CommandID.PING, new PingPacket(DateTime.Now.Ticks));
        }

        /// <summary> Deserialize response. </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <param name="result"> [out] The result. </param>
        /// <returns> A TResult. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeserializeResponse<TResult>(in Packet packet, out TResult result)
            where TResult : unmanaged
        {
            packet.Buffer.FromBytesUnsafe(packet.Offset, out result);
            return true;
        }

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="commandOrResponseID"> The command or response id. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length. </param>
        /// <param name="requestID">           Identifier for the request. </param>
        /// <param name="isResponse">          Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        private unsafe SendError BeginSend(ushort commandOrResponseID,
                                           byte[] data,
                                           int    offset,
                                           int    length,
                                           ushort requestID,
                                           bool   isResponse)
        {
            if (_clientSocket == null || (_state & SEND_FLAG) != SEND_FLAG) { return SendError.Invalid; }

            PacketInfo packetInfo;
            packetInfo.CommandOrResponseID = commandOrResponseID;
            packetInfo.RequestID           = requestID;
            packetInfo.IsResponse          = isResponse;
            packetInfo.Length              = length;
            packetInfo.CompressedLength    = length;
            packetInfo.CompressionMode     = CompressionMode.None;
            if (length >= Constants.LENGTH_THRESHOLD && _compressionMode != CompressionMode.None)
            {
                byte[] buffer = new byte[LZ4Codec.MaximumOutputSize(length)];

                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                int s = _compressionMode switch
                {
                    CompressionMode.Lz4 => LZ4Codec.Encode(data, offset, length, buffer, 0, buffer.Length),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(_compressionMode), _compressionMode, "Not supported!")
                };
                if (s > 0 && s < length)
                {
                    packetInfo.CompressedLength = s;
                    packetInfo.CompressionMode  = _compressionMode;
                    data                        = buffer;
                    offset                      = 0;
                }
            }

            fixed (byte* src = data)
            {
                packetInfo.Src = src + offset;
                if (packetInfo.CompressedLength <= MaxPayloadSize)
                {
                    packetInfo.PacketID    = 0;
                    packetInfo.ChunkOffset = 0;
                    packetInfo.ChunkLength = packetInfo.CompressedLength;
                    packetInfo.IsChunked   = false;
                    return BeginSend(in packetInfo);
                }
                packetInfo.PacketID    = Interlocked.Increment(ref _packetID);
                packetInfo.ChunkOffset = 0;
                packetInfo.IsChunked   = true;
                int chunkLength = packetInfo.CompressedLength;
                while (chunkLength > MaxPayloadSize)
                {
                    packetInfo.ChunkLength = MaxPayloadSize;
                    SendError se = BeginSend(in packetInfo);
                    if (se != SendError.None)
                    {
                        return se;
                    }
                    chunkLength            -= MaxPayloadSize;
                    packetInfo.ChunkOffset += MaxPayloadSize;
                }
                packetInfo.ChunkLength = chunkLength;
                return BeginSend(in packetInfo);
            }
        }

        private unsafe SendError SendConnect()
        {
            ConnectPacket packet;
            fixed (byte* ptr = _connectChecksum)
            {
                Mem.Cpy(packet.Checksum, ptr, sizeof(byte) * 16);
            }
            packet.Rejected = false;
            packet.Nonce    = 0;
            return Send(CommandID.CONNECT, packet);
        }

        private protected abstract SendError BeginSend(in PacketInfo packetInfo);
    }
}