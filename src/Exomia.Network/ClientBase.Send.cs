#region License

// Copyright (c) 2018-2020, exomia
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
        public SendError Send(uint commandID, byte[] data, int offset, int length, uint responseID = 0)
        {
            return BeginSend(commandID, data, offset, length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError Send(uint commandID, byte[] data, uint responseID = 0)
        {
            return BeginSend(commandID, data, 0, data.Length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError Send<T>(uint commandID, in T data, uint responseID = 0) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return BeginSend(commandID, dataB, 0, length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError Send(uint commandID, ISerializable serializable, uint responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return BeginSend(commandID, dataB, 0, length, 0, responseID);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                            byte[]                            data,
                                                            int                               offset,
                                                            int                               length,
                                                            DeserializePacketHandler<TResult> deserialize,
                                                            TimeSpan                          timeout,
                                                            uint                              responseID = 0)
        {
            TaskCompletionSource<(uint requestID, Packet packet)> tcs =
                new TaskCompletionSource<(uint, Packet)>(TaskCreationOptions.None);
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);
            uint                          requestID;
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
            SendError sendError = BeginSend(commandID, data, offset, length, requestID, responseID);
            if (sendError == SendError.None)
            {
                (uint rID, Packet packet) = await tcs.Task;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (packet.Buffer != null)
                {
                    TResult result = deserialize(in packet);
                    ByteArrayPool.Return(packet.Buffer);
                    return new Response<TResult>(result, rID, SendError.None);
                }
                sendError = SendError.Unknown; //TimeOut Error
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
        public Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, uint responseID = 0)
            where TResult : unmanaged
        {
            return SendR(commandID, data, 0, data.Length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint   commandID,
                                                      byte[] data,
                                                      int    offset,
                                                      int    length,
                                                      uint   responseID = 0)
            where TResult : unmanaged
        {
            return SendR(commandID, data, offset, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      int                               offset,
                                                      int                               length,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      uint                              responseID = 0)
        {
            return SendR(commandID, data, offset, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      uint                              responseID = 0)
        {
            return SendR(commandID, data, 0, data.Length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint     commandID,
                                                      byte[]   data,
                                                      int      offset,
                                                      int      length,
                                                      TimeSpan timeout,
                                                      uint     responseID = 0)
            where TResult : unmanaged
        {
            return SendR(commandID, data, offset, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint     commandID,
                                                      byte[]   data,
                                                      TimeSpan timeout,
                                                      uint     responseID = 0)
            where TResult : unmanaged
        {
            return SendR(commandID, data, 0, data.Length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      byte[]                            data,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout,
                                                      uint                              responseID = 0)
        {
            return SendR(commandID, data, 0, data.Length, deserialize, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable, uint responseID = 0)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      uint                              responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint          commandID,
                                                      ISerializable serializable,
                                                      TimeSpan      timeout,
                                                      uint          responseID = 0)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                                      ISerializable                     serializable,
                                                      DeserializePacketHandler<TResult> deserialize,
                                                      TimeSpan                          timeout,
                                                      uint                              responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendR(commandID, dataB, 0, length, deserialize, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data, uint responseID = 0)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                         in T                              data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         uint                              responseID = 0)
            where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint     commandID,
                                                         in T     data,
                                                         TimeSpan timeout,
                                                         uint     responseID = 0)
            where T : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendR<T, TResult>(uint commandID,
                                                         in T data,
                                                         DeserializePacketHandler<TResult> deserialize,
                                                         TimeSpan timeout,
                                                         uint responseID = 0) where T : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendR(commandID, dataB, 0, length, deserialize, timeout, responseID);
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

        /// <summary>
        ///     Deserialize response.
        /// </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <returns>
        ///     A TResult.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult DeserializeResponse<TResult>(in Packet packet)
            where TResult : unmanaged
        {
            return packet.Buffer.FromBytesUnsafe2<TResult>(packet.Offset);
        }

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="commandID">  The command id. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="requestID">  Identifier for the request. </param>
        /// <param name="responseID"> Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        private unsafe SendError BeginSend(uint   commandID,
                                           byte[] data,
                                           int    offset,
                                           int    length,
                                           uint   requestID,
                                           uint   responseID)
        {
            if (_clientSocket == null || (_state & SEND_FLAG) != SEND_FLAG) { return SendError.Invalid; }

            PacketInfo packetInfo;
            packetInfo.CommandID        = commandID;
            packetInfo.RequestID        = requestID;
            packetInfo.ResponseID       = responseID;
            packetInfo.Length           = length;
            packetInfo.CompressedLength = length;
            packetInfo.CompressionMode  = CompressionMode.None;
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

        /// <summary>
        ///     Sends the connect.
        /// </summary>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private unsafe SendError SendConnect()
        {
            ConnectPacket packet;
            fixed (byte* ptr = _connectChecksum)
            {
                Mem.Cpy(packet.Checksum, ptr, sizeof(byte) * 16);
            }
            return Send(CommandID.CONNECT, packet);
        }

        /// <summary>
        ///     Begins send data.
        /// </summary>
        /// <param name="packetInfo"> Information describing the packet. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private protected abstract SendError BeginSend(in PacketInfo packetInfo);
    }
}