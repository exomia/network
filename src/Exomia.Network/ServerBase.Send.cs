﻿#region License

// Copyright (c) 2018-2021, exomia
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
                                ushort        commandOrResponseID,
                                byte[]        data,
                                int           offset,
                                int           length,
                                bool          isResponse = false)
        {
            return SendTo(client.Arg0, commandOrResponseID, data, offset, length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                ushort        commandOrResponseID,
                                byte[]        data,
                                bool          isResponse = false)
        {
            return SendTo(client.Arg0, commandOrResponseID, data, 0, data.Length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError SendTo(TServerClient client,
                                ushort        commandOrResponseID,
                                ISerializable serializable,
                                bool          isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendTo(client.Arg0, commandOrResponseID, dataB, 0, length, 0, isResponse);
        }

        /// <inheritdoc />
        public SendError SendTo<T1>(TServerClient client,
                                    ushort        commandOrResponseID,
                                    in T1         data,
                                    bool          isResponse = false)
            where T1 : unmanaged
        {
            byte[] dataB = data.ToBytesUnsafe2(out int length);
            return SendTo(client.Arg0, commandOrResponseID, dataB, 0, length, 0, isResponse);
        }

        /// <inheritdoc />
        public async Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                              ushort                            commandOrResponseID,
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
            SendError sendError = SendTo(client.Arg0, commandOrResponseID, data, offset, length, requestID, isResponse);
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
                                                        ushort        commandOrResponseID,
                                                        byte[]        data,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, 0, data.Length, DeserializeResponse, s_defaultTimeout,
                isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        ushort        commandOrResponseID,
                                                        byte[]        data,
                                                        int           offset,
                                                        int           length,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, offset, length, DeserializeResponse, s_defaultTimeout,
                isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        ushort                            commandOrResponseID,
                                                        byte[]                            data,
                                                        int                               offset,
                                                        int                               length,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        bool                              isResponse = false)
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, offset, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        ushort                            commandOrResponseID,
                                                        byte[]                            data,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        bool                              isResponse = false)
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, 0, data.Length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        ushort        commandOrResponseID,
                                                        byte[]        data,
                                                        int           offset,
                                                        int           length,
                                                        TimeSpan      timeout,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, offset, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        ushort        commandOrResponseID,
                                                        byte[]        data,
                                                        TimeSpan      timeout,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, 0, data.Length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        ushort                            commandOrResponseID,
                                                        byte[]                            data,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        TimeSpan                          timeout,
                                                        bool                              isResponse = false)
        {
            return SendToR<TResult>(
                client, commandOrResponseID, data, 0, data.Length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        ushort        commandOrResponseID,
                                                        ISerializable serializable,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, DeserializeResponse, s_defaultTimeout,
                isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        ushort                            commandOrResponseID,
                                                        ISerializable                     serializable,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        bool                              isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                        ushort        commandOrResponseID,
                                                        ISerializable serializable,
                                                        TimeSpan      timeout,
                                                        bool          isResponse = false)
            where TResult : unmanaged
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                        ushort                            commandOrResponseID,
                                                        ISerializable                     serializable,
                                                        DeserializePacketHandler<TResult> deserialize,
                                                        TimeSpan                          timeout,
                                                        bool                              isResponse = false)
        {
            byte[] dataB = serializable.Serialize(out int length);
            return SendToR<TResult>(client, commandOrResponseID, dataB, 0, length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                ushort        commandOrResponseID,
                                                                in TValue     data,
                                                                bool          isResponse = false)
            where TValue : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, DeserializeResponse, s_defaultTimeout,
                isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient                     client,
                                                                ushort                            commandOrResponseID,
                                                                in TValue                         data,
                                                                DeserializePacketHandler<TResult> deserialize,
                                                                bool                              isResponse = false)
            where TValue : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, deserialize, s_defaultTimeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                ushort        commandOrResponseID,
                                                                in TValue     data,
                                                                TimeSpan      timeout,
                                                                bool          isResponse = false)
            where TValue : unmanaged
            where TResult : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR<TResult>(
                client, commandOrResponseID, dataB, 0, length, DeserializeResponse, timeout, isResponse);
        }

        /// <inheritdoc />
        public Task<Response<TResult>> SendToR<TValue, TResult>(TServerClient client,
                                                                ushort commandOrResponseID,
                                                                in TValue data,
                                                                DeserializePacketHandler<TResult> deserialize,
                                                                TimeSpan timeout,
                                                                bool isResponse = false) where TValue : unmanaged
        {
            data.ToBytesUnsafe2(out byte[] dataB, out int length);
            return SendToR<TResult>(client, commandOrResponseID, dataB, 0, length, deserialize, timeout, isResponse);
        }

        /// <inheritdoc />
        public void SendToAll(ushort         commandOrResponseID,
                              byte[]         data,
                              int            offset,
                              int            length,
                              TServerClient? exclude = null)
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
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (T arg0 in clients.Keys)
                {
                    if (exclude == null || exclude.Arg0 != arg0)
                    {
                        SendTo(arg0, commandOrResponseID, data, offset, length);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void SendToAll(ushort commandOrResponseID, byte[] data, TServerClient? exclude = null)
        {
            SendToAll(commandOrResponseID, data, 0, data.Length, exclude);
        }

        /// <inheritdoc />
        public void SendToAll<T1>(ushort commandOrResponseID, in T1 data, TServerClient? exclude = null)
            where T1 : unmanaged
        {
            byte[] buffer = data.ToBytesUnsafe2(out int length);
            SendToAll(commandOrResponseID, buffer, 0, length, exclude);
        }

        /// <inheritdoc />
        public void SendToAll(ushort commandOrResponseID, ISerializable serializable, TServerClient? exclude = null)
        {
            byte[] buffer = serializable.Serialize(out int length);
            SendToAll(commandOrResponseID, buffer, 0, length, exclude);
        }

        /// <summary> Deserialize response. </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <param name="result"> [out] The result. </param>
        /// <returns> True if it succeeds, false if it fails. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DeserializeResponse<TResult>(in Packet packet, out TResult result)
            where TResult : unmanaged
        {
            packet.Buffer.FromBytesUnsafe(packet.Offset, out result);
            return true;
        }

        private unsafe SendError SendTo(T      arg0,
                                        ushort commandOrResponseID,
                                        byte[] data,
                                        int    offset,
                                        int    length,
                                        ushort requestID  = 0,
                                        bool   isResponse = false)
        {
            if (_listener == null || (_state & SEND_FLAG) != SEND_FLAG) { return SendError.Invalid; }

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

        private protected abstract SendError BeginSendTo(T             arg0,
                                                         in PacketInfo packetInfo);
    }
}