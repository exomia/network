#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network.Buffers;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Serialization;
using K4os.Compression.LZ4;

namespace Exomia.Network
{
    public abstract partial class ServerBase<T, TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                byte[]        data,
                                int           offset,
                                int           length,
                                uint          responseID = 0)
        {
            return SendTo(client.Arg0, commandID, data, offset, length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                byte[]        data,
                                uint          responseID = 0)
        {
            return SendTo(client.Arg0, commandID, data, 0, data.Length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                uint          commandID,
                                ISerializable serializable,
                                uint          responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendTo(client.Arg0, commandID, dataB, 0, length, 0, responseID);
        }

        /// <inheritdoc />
        public SendError SendTo<T1>(TServerClient client,
                                    uint          commandID,
                                    in T1         data,
                                    uint          responseID = 0)
            where T1 : unmanaged
        {
            byte[] dataB = data.ToBytesUnsafe2(out int length);
            return SendTo(client.Arg0, commandID, dataB, 0, length, 0, responseID);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                              uint                              commandID,
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
            SendError sendError = SendTo(client.Arg0, commandID, data, offset, length, requestID, responseID);
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
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        byte[]        data,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            return SendToR(
                client, commandID, data, 0, data.Length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        byte[]        data,
                                                        int           offset,
                                                        int           length,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            return SendToR(
                client, commandID, data, offset, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        uint                              commandID,
                                                        byte[]                            data,
                                                        int                               offset,
                                                        int                               length,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        uint                              responseID = 0)
        {
            return SendToR(client, commandID, data, offset, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        uint                              commandID,
                                                        byte[]                            data,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        uint                              responseID = 0)
        {
            return SendToR(client, commandID, data, 0, data.Length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        byte[]        data,
                                                        int           offset,
                                                        int           length,
                                                        TimeSpan      timeout,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            return SendToR(client, commandID, data, offset, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        byte[]        data,
                                                        TimeSpan      timeout,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            return SendToR(client, commandID, data, 0, data.Length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        uint                              commandID,
                                                        byte[]                            data,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        TimeSpan                          timeout,
                                                        uint                              responseID = 0)
        {
            return SendToR(client, commandID, data, 0, data.Length, deserialize, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        ISerializable serializable,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR(
                client, commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        uint                              commandID,
                                                        ISerializable                     serializable,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        uint                              responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR(client, commandID, dataB, 0, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        uint          commandID,
                                                        ISerializable serializable,
                                                        TimeSpan      timeout,
                                                        uint          responseID = 0)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR(client, commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        uint                              commandID,
                                                        ISerializable                     serializable,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        TimeSpan                          timeout,
                                                        uint                              responseID = 0)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR(client, commandID, dataB, 0, length, deserialize, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                uint          commandID,
                                                                in TValue     data,
                                                                uint          responseID = 0)
            where TValue : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR(
                client, commandID, dataB, 0, length, DeserializeResponse<TResult>, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient                     client,
                                                                uint                              commandID,
                                                                in TValue                         data,
                                                                DeserializePacketHandler<TResult> deserialize,
                                                                uint                              responseID = 0)
            where TValue : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR(client, commandID, dataB, 0, length, deserialize, s_defaultTimeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                uint          commandID,
                                                                in TValue     data,
                                                                TimeSpan      timeout,
                                                                uint          responseID = 0)
            where TValue : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR(client, commandID, dataB, 0, length, DeserializeResponse<TResult>, timeout, responseID);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                uint commandID,
                                                                in TValue data,
                                                                DeserializePacketHandler<TResult> deserialize,
                                                                TimeSpan timeout,
                                                                uint responseID = 0) where TValue : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR(client, commandID, dataB, 0, length, deserialize, timeout, responseID);
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, byte[] data, int offset, int length)
        {
            Dictionary<T, TServerClient> clients;
            bool                         lockTaken = false;
            try
            {
                _clientsLock.Enter(ref lockTaken);
                clients = new Dictionary<T, TServerClient>(_clients);
            }
            finally
            {
                if (lockTaken) { _clientsLock.Exit(false); }
            }

            if (clients.Count > 0)
            {
                foreach (T arg0 in clients.Keys)
                {
                    SendTo(arg0, commandID, data, offset, length);
                }
            }
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, byte[] data)
        {
            SendToAll(commandID, data, 0, data.Length);
        }

        /// <inheritdoc />
        public void SendToAll<T1>(uint commandID, in T1 data)
            where T1 : unmanaged
        {
            byte[] buffer = data.ToBytesUnsafe2(out int length);
            SendToAll(commandID, buffer, 0, length);
        }

        /// <inheritdoc />
        public void SendToAll(uint commandID, ISerializable serializable)
        {
            byte[] buffer = serializable.Serialize(out int length);
            SendToAll(commandID, buffer, 0, length);
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

        private unsafe SendError SendTo(T      arg0,
                                        uint   commandID,
                                        byte[] data,
                                        int    offset,
                                        int    length,
                                        uint   requestID  = 0,
                                        uint   responseID = 0)
        {
            if (_listener == null || (_state & SEND_FLAG) != SEND_FLAG) { return SendError.Invalid; }

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
                    return BeginSendTo(arg0, in packetInfo);
                }

                packetInfo.PacketID    = Interlocked.Increment(ref _packetID);
                packetInfo.ChunkOffset = 0;
                packetInfo.IsChunked   = true;
                int chunkLength = packetInfo.CompressedLength;
                while (chunkLength > MaxPayloadSize)
                {
                    packetInfo.ChunkLength = MaxPayloadSize;
                    SendError se = BeginSendTo(arg0, in packetInfo);
                    if (se != SendError.None)
                    {
                        return se;
                    }
                    chunkLength            -= MaxPayloadSize;
                    packetInfo.ChunkOffset += MaxPayloadSize;
                }
                packetInfo.ChunkLength = chunkLength;
                return BeginSendTo(arg0, in packetInfo);
            }
        }

        /// <summary>
        ///     Begins send data to.
        /// </summary>
        /// <param name="arg0">       The argument 0. </param>
        /// <param name="packetInfo"> Information describing the packet. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        private protected abstract SendError BeginSendTo(T             arg0,
                                                         in PacketInfo packetInfo);
    }
}