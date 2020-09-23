#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Diagnostics.CodeAnalysis;
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using K4os.Compression.LZ4;

namespace Exomia.Network.Serialization
{
    /// UDP HEADER LAYOUT
    /// 8bit
    /// | IS_CHUNKED BIT | REQUEST BIT | RESPONSE BIT | COMPRESSED MODE | ENCRYPT MODE |
    /// | 7              | 6           | 6            | 4  3            | 2  1  0      |
    /// | VR: 0/1        | VR: 0/1     | VR: 0/1      | VR: 0-8         | VR: 0-8      | VR = VALUE RANGE
    /// -------------------------------------------------------------------------------------------------------------------------------------
    /// | 0              | 0           | 0            | 0  0            | 1  1  1      | ENCRYPT_MODE_MASK    0b00000111
    /// | 0              | 0           | 0            | 1  1            | 0  0  0      | COMPRESSED_MODE_MASK 0b00011000
    /// | 0              | 0           | 1            | 0  0            | 0  0  0      | RESPONSE_BIT_MASK    0b00100000
    /// | 0              | 1           | 0            | 0  0            | 0  0  0      | REQUEST_BIT_MASK     0b01000000
    /// | 1              | 0           | 0            | 0  0            | 0  0  0      | IS_CHUNKED_BIT_MASK  0b10000000
    /// 32bit
    /// | COMMANDID OR RESPONSEID 31-16 (16)bit            | DATA LENGTH 15-0 (16)bit                        |
    /// | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
    /// | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE_RANGE
    /// --------------------------------------------------------------------------------------------------------------------------------
    /// |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
    /// |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000.
    /// <content>
    ///     A UDP serialization helper class.
    /// </content>
    static unsafe partial class Serialization
    {
        internal static int SerializeUdp(in PacketInfo  packetInfo,
                                         byte*          dst,
                                         EncryptionMode encryptionMode)
        {
            *dst = (byte)encryptionMode;

            if (packetInfo.IsResponse)
            {
                *dst |= Constants.RESPONSE_1_BIT;
            }

            int offset = 0;
            if (packetInfo.RequestID != 0)
            {
                *dst                                      |= Constants.REQUEST_1_BIT;
                *(uint*)(dst + Constants.UDP_HEADER_SIZE) =  packetInfo.RequestID;
                offset                                    =  Constants.OFFSET_REQUEST_ID;
            }

            if (packetInfo.CompressionMode != CompressionMode.None)
            {
                *dst                                              |= (byte)packetInfo.CompressionMode;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset) =  packetInfo.Length;
                offset                                            += Constants.OFFSET_COMPRESSION_MODE;
            }

            if (packetInfo.IsChunked)
            {
                *dst                                                  |= Constants.IS_CHUNKED_1_BIT;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset)     =  packetInfo.PacketID;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset + 4) =  packetInfo.ChunkOffset;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset + 8) =  packetInfo.CompressedLength;
                offset                                                += Constants.OFFSET_CHUNK_INFO;
            }

            *(uint*)(dst + 1) = ((uint)(packetInfo.ChunkLength + offset) & Constants.DATA_LENGTH_MASK)
                              | (packetInfo.CommandOrResponseID << Constants.COMMAND_OR_RESPONSE_ID_SHIFT);
            Mem.Cpy(
                dst + Constants.UDP_HEADER_SIZE + offset,
                packetInfo.Src + packetInfo.ChunkOffset,
                packetInfo.ChunkLength);
            return Constants.UDP_HEADER_SIZE + offset + packetInfo.ChunkLength;
        }

        internal static bool DeserializeUdp<TKey>(byte[]               buffer,
                                                  int                  bytesTransferred,
                                                  BigDataHandler<TKey> bigDataHandler,
                                                  Func<int, TKey>      keyFunc,
#if NETSTANDARD2_1
                                                  [NotNullWhen(true)] out DeserializePacketInfo deserializePacketInfo)
#else
                                                  out DeserializePacketInfo deserializePacketInfo)
#endif
            where TKey : struct
        {
            fixed (byte* src = buffer)
            {
                byte packetHeader = *src;
                uint h2           = *(uint*)(src + 1);
                deserializePacketInfo.CommandOrResponseID = h2 >> Constants.COMMAND_OR_RESPONSE_ID_SHIFT;
                deserializePacketInfo.Length              = (int)(h2 & Constants.DATA_LENGTH_MASK);
                deserializePacketInfo.RequestID           = 0;
                deserializePacketInfo.IsResponseBitSet    = (packetHeader & Constants.RESPONSE_1_BIT) != 0;

                if (bytesTransferred == deserializePacketInfo.Length + Constants.UDP_HEADER_SIZE)
                {
                    int offset = 0;
                    if ((packetHeader & Constants.REQUEST_1_BIT) != 0)
                    {
                        deserializePacketInfo.RequestID = *(uint*)(src + Constants.UDP_HEADER_SIZE);
                        offset                          = Constants.OFFSET_REQUEST_ID;
                    }

                    int l = 0;
                    CompressionMode compressionMode =
                        (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK);
                    if (compressionMode != CompressionMode.None)
                    {
                        l      =  *(int*)(src + Constants.UDP_HEADER_SIZE + offset);
                        offset += Constants.OFFSET_COMPRESSION_MODE;
                    }

                    if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                    {
                        int cl = *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 8);
                        byte[]? bdb = bigDataHandler.Receive(
                            keyFunc(*(int*)(src + Constants.UDP_HEADER_SIZE + offset)),
                            src + Constants.UDP_HEADER_SIZE + offset + Constants.OFFSET_CHUNK_INFO,
                            deserializePacketInfo.Length - offset - Constants.OFFSET_CHUNK_INFO,
                            *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 4),
                            cl);
                        deserializePacketInfo.Length = cl;
                        if (bdb != null)
                        {
                            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                            switch (compressionMode)
                            {
                                case CompressionMode.None:
                                    deserializePacketInfo.Data = bdb;
                                    return true;
                                case CompressionMode.Lz4:
                                    fixed (byte* srcB = bdb)
                                    fixed (byte* dst = deserializePacketInfo.Data = ByteArrayPool.Rent(l))
                                    {
                                        deserializePacketInfo.Length = LZ4Codec.Decode(
                                            srcB, deserializePacketInfo.Length, dst, l);
                                        if (deserializePacketInfo.Length != l)
                                        {
                                            throw new Exception("LZ4.Decode FAILED!");
                                        }
                                    }
                                    return true;
                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(CompressionMode), compressionMode, "Not supported!");
                            }
                        }
                    }
                    else
                    {
                        deserializePacketInfo.Length -= offset;

                        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                        switch (compressionMode)
                        {
                            case CompressionMode.None:
                                fixed (byte* dst = deserializePacketInfo.Data =
                                    ByteArrayPool.Rent(deserializePacketInfo.Length))
                                {
                                    Mem.Cpy(
                                        dst, src + Constants.UDP_HEADER_SIZE + offset, deserializePacketInfo.Length);
                                }
                                return true;
                            case CompressionMode.Lz4:
                                fixed (byte* dst = deserializePacketInfo.Data = ByteArrayPool.Rent(l))
                                {
                                    deserializePacketInfo.Length = LZ4Codec.Decode(
                                        src + Constants.UDP_HEADER_SIZE + offset, deserializePacketInfo.Length, dst, l);
                                    if (deserializePacketInfo.Length != l)
                                    {
                                        throw new Exception("LZ4.Decode FAILED!");
                                    }
                                }
                                return true;
                            default:
                                throw new ArgumentOutOfRangeException(
                                    nameof(CompressionMode), compressionMode, "Not supported!");
                        }
                    }
                }
            }
            deserializePacketInfo.Data = null!; //can be null, but it doesn't matter we don't' use it anyway.
            return false;
        }
    }
}