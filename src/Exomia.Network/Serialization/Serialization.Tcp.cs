#region License

// Copyright (c) 2018-2019, exomia
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
    /// | IS_CHUNKED BIT | RESPONSE BIT | COMPRESSED MODE | ENCRYPT MODE |
    /// | 7              | 6            | 5  4  3         | 2  1  0      |
    /// | VR: 0/1        | VR: 0/1      | VR: 0-8         | VR: 0-8      | VR = VALUE RANGE
    /// -----------------------------------------------------------------------------------------------------------------------
    /// | 0              | 0            | 0  0  0         | 1  1  1      | ENCRYPT_MODE_MASK    0b00000111
    /// | 0              | 0            | 1  1  1         | 0  0  0      | COMPRESSED_MODE_MASK 0b00111000
    /// | 0              | 1            | 0  0  0         | 0  0  0      | RESPONSE_BIT_MASK    0b01000000
    /// | 1              | 0            | 0  0  0         | 0  0  0      | IS_CHUNKED_BIT_MASK  0b10000000
    /// 32bit
    /// | COMMANDID 31-16 (16)bit                          | DATA LENGTH 15-0 (16)bit                        |
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
        /// <summary>
        ///     Serialize TCP.
        /// </summary>
        /// <param name="packetInfo">      Identifier for the packet. </param>
        /// <param name="dst">             [in,out] If non-null, destination for the. </param>
        /// <param name="encryptionMode">  The encryption mode. </param>
        /// <returns>
        ///     An int.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SerializeTcp(in PacketInfo  packetInfo,
                                         byte*          dst,
                                         EncryptionMode encryptionMode)
        {
            *dst = (byte)encryptionMode;

            int offset = 0;
            if (packetInfo.ResponseID != 0)
            {
                *dst                                      |= Constants.RESPONSE_1_BIT;
                *(uint*)(dst + Constants.TCP_HEADER_SIZE) =  packetInfo.ResponseID;
                offset                                    =  4;
            }

            if (packetInfo.CompressionMode != CompressionMode.None)
            {
                *dst                                              |= (byte)packetInfo.CompressionMode;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset) =  packetInfo.Length;
                offset                                            += 4;
            }

            if (packetInfo.IsChunked)
            {
                *dst                                                  |= Constants.IS_CHUNKED_1_BIT;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset)     =  packetInfo.PacketID;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + 4) =  packetInfo.ChunkOffset;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + 8) =  packetInfo.CompressedLength;
                offset                                                += 12;
            }

            ushort checksum = PayloadEncoding.Encode(
                packetInfo.Src + packetInfo.ChunkOffset, packetInfo.ChunkLength,
                dst + Constants.TCP_HEADER_SIZE + offset, out int l);

            *(uint*)(dst + 1) =
                ((uint)(l + offset + 1) & Constants.DATA_LENGTH_MASK) |
                (packetInfo.CommandID << Constants.COMMAND_ID_SHIFT);
            *(ushort*)(dst + 5)                                   = checksum;
            *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + l) = Constants.ZERO_BYTE;

            return Constants.TCP_HEADER_SIZE + offset + l + 1;
        }

        /// <summary>
        ///     Deserialize TCP.
        /// </summary>
        /// <param name="packetHeader">   The packet header. </param>
        /// <param name="checksum">       The checksum. </param>
        /// <param name="source">         Source for the. </param>
        /// <param name="bigDataHandler"> The big data handler. </param>
        /// <param name="payload">        [out] The payload. </param>
        /// <param name="length">         [in,out] The length. </param>
        /// <param name="responseID">     [out] Identifier for the response. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// <exception cref="Exception">                   Thrown when an exception error condition occurs. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        internal static bool DeserializeTcp(byte           packetHeader,
                                            ushort         checksum,
                                            byte[]         source,
                                            BigDataHandler bigDataHandler,
                                            out byte[]     payload,
                                            ref int        length,
                                            out uint       responseID)
        {
            fixed (byte* ptr = source)
            {
                responseID = 0;
                int offset = 0;
                if ((packetHeader & Constants.RESPONSE_BIT_MASK) != 0)
                {
                    responseID = *(uint*)(ptr + offset);
                    offset     = 4;
                }

                int l = 0;
                CompressionMode compressionMode =
                    (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK);
                if (compressionMode != CompressionMode.None)
                {
                    l      =  *(int*)(ptr + offset);
                    offset += 4;
                }

                int packetId    = 0;
                int chunkOffset = 0;
                int cl          = 0;
                if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                {
                    packetId    = *(int*)(ptr + offset);
                    chunkOffset = *(int*)(ptr + offset + 4);
                    cl          = *(int*)(ptr + offset + 8);

                    offset += 12;
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