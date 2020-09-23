#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Runtime.CompilerServices;
using Exomia.Network.Buffers;
using Exomia.Network.Encoding;
using K4os.Compression.LZ4;

namespace Exomia.Network.Serialization
{
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
    /// |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |DATA_LENGTH_MASK 0xFFFF
    /// |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |COMMANDID_MASK 0xFFFF0000
    /// 16bit   -    CHECKSUM
    /// <content>
    ///     A TCP serialization helper class.
    /// </content>
    static unsafe partial class Serialization
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SerializeTcp(in PacketInfo  packetInfo,
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
                *(uint*)(dst + Constants.TCP_HEADER_SIZE) =  packetInfo.RequestID;
                offset                                    =  Constants.OFFSET_REQUEST_ID;
            }

            if (packetInfo.CompressionMode != CompressionMode.None)
            {
                *dst                                              |= (byte)packetInfo.CompressionMode;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset) =  packetInfo.Length;
                offset                                            += Constants.OFFSET_COMPRESSION_MODE;
            }

            if (packetInfo.IsChunked)
            {
                *dst                                                  |= Constants.IS_CHUNKED_1_BIT;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset)     =  packetInfo.PacketID;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + 4) =  packetInfo.ChunkOffset;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + 8) =  packetInfo.CompressedLength;
                offset                                                += Constants.OFFSET_CHUNK_INFO;
            }

            ushort checksum = PayloadEncoding.Encode(
                packetInfo.Src + packetInfo.ChunkOffset, packetInfo.ChunkLength,
                dst + Constants.TCP_HEADER_SIZE + offset, out int l);

            *(uint*)(dst + 1) = ((uint)(l + offset + 1) & Constants.DATA_LENGTH_MASK)
                              | (packetInfo.CommandOrResponseID << Constants.COMMAND_OR_RESPONSE_ID_SHIFT);
            *(ushort*)(dst + 5)                                   = checksum;
            *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + l) = Constants.ZERO_BYTE;

            return Constants.TCP_HEADER_SIZE + offset + l + 1;
        }

        internal static bool DeserializeTcp(byte                packetHeader,
                                            ushort              checksum,
                                            byte[]              source,
                                            BigDataHandler<int> bigDataHandler,
                                            out byte[]          payload,
                                            ref int             length,
                                            out uint            requestID,
                                            out bool            isResponseBitSet)
        {
            fixed (byte* ptr = source)
            {
                requestID = 0;

                int offset = 0;
                if ((packetHeader & Constants.REQUEST_1_BIT) != 0)
                {
                    requestID = *(uint*)ptr;
                    offset    = Constants.OFFSET_REQUEST_ID;
                }

                isResponseBitSet = (packetHeader & Constants.RESPONSE_1_BIT) != 0;

                int l = 0;
                CompressionMode compressionMode =
                    (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK);
                if (compressionMode != CompressionMode.None)
                {
                    l      =  *(int*)(ptr + offset);
                    offset += Constants.OFFSET_COMPRESSION_MODE;
                }

                int packetId    = 0;
                int chunkOffset = 0;
                int cl          = 0;
                if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                {
                    packetId    =  *(int*)(ptr + offset);
                    chunkOffset =  *(int*)(ptr + offset + 4);
                    cl          =  *(int*)(ptr + offset + 8);
                    offset      += Constants.OFFSET_CHUNK_INFO;
                }

                fixed (byte* dst = payload = ByteArrayPool.Rent(length))
                {
                    if (PayloadEncoding.Decode(
                        ptr + offset, length - offset - 1, dst,
                        out length) != checksum)
                    {
                        return false;
                    }

                    if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                    {
                        byte[]? buffer = bigDataHandler.Receive(
                            packetId, dst, length, chunkOffset, cl);
                        if (buffer == null)
                        {
                            return false;
                        }
                        payload = buffer;
                        length  = cl;
                    }
                }

                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (compressionMode)
                {
                    case CompressionMode.None:
                        return true;
                    case CompressionMode.Lz4:
                        byte[] buffer = ByteArrayPool.Rent(l);
                        fixed (byte* bPtr = buffer)
                        fixed (byte* dst = payload)
                        {
                            length =
                                LZ4Codec.Decode(dst, length, bPtr, l);
                        }
                        if (length != l)
                        {
                            throw new Exception("LZ4.Decode FAILED!");
                        }
                        ByteArrayPool.Return(payload);
                        payload = buffer;
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(CompressionMode), compressionMode, "Not supported!");
                }
            }
        }
    }
}