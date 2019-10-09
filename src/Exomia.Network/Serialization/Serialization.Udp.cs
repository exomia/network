#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using Exomia.Network.Buffers;
using Exomia.Network.Native;
using K4os.Compression.LZ4;

namespace Exomia.Network.Serialization
{
    /// UDP HEADER LAYOUT
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
    /// |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
    /// |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000.
    /// <content>
    ///     A UDP serialization helper class.
    /// </content>
    static unsafe partial class Serialization
    {
        /// <summary>
        ///     Serialize UDP.
        /// </summary>
        /// <param name="packetInfo">      Information describing the packet. </param>
        /// <param name="dst">             [in,out] If non-null, destination for the. </param>
        /// <param name="encryptionMode">  The encryption mode. </param>
        /// <returns>
        ///     An int.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        internal static int SerializeUdp(in PacketInfo  packetInfo,
                                         byte*          dst,
                                         EncryptionMode encryptionMode)
        {
            *dst = (byte)encryptionMode;

            int offset = 0;
            if (packetInfo.ResponseID != 0)
            {
                *dst                                      |= Constants.RESPONSE_1_BIT;
                *(uint*)(dst + Constants.UDP_HEADER_SIZE) =  packetInfo.ResponseID;
                offset                                    =  4;
            }

            if (packetInfo.CompressionMode != CompressionMode.None)
            {
                *dst                                              |= (byte)packetInfo.CompressionMode;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset) =  packetInfo.Length;
                offset                                            += 4;
            }

            if (packetInfo.IsChunked)
            {
                *dst                                                  |= Constants.IS_CHUNKED_1_BIT;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset)     =  packetInfo.PacketID;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset + 4) =  packetInfo.ChunkOffset;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + offset + 8) =  packetInfo.CompressedLength;
                offset                                                += 12;
            }

            *(uint*)(dst + 1) =
                ((uint)(packetInfo.ChunkLength + offset) & Constants.DATA_LENGTH_MASK) |
                (packetInfo.CommandID << Constants.COMMAND_ID_SHIFT);
            Mem.Cpy(
                dst + Constants.UDP_HEADER_SIZE + offset,
                packetInfo.Src + packetInfo.ChunkOffset,
                packetInfo.ChunkLength);
            return Constants.UDP_HEADER_SIZE + offset + packetInfo.ChunkLength;
        }

        internal static bool DeserializeUdp(byte[]                    buffer,
                                            int                       bytesTransferred,
                                            BigDataHandler            bigDataHandler,
                                            out DeserializePacketInfo deserializePacketInfo)
        {
            fixed (byte* src = buffer)
            {
                byte packetHeader = *src;
                uint h2           = *(uint*)(src + 1);
                deserializePacketInfo.CommandID  = h2 >> Constants.COMMAND_ID_SHIFT;
                deserializePacketInfo.Length     = (int)(h2 & Constants.DATA_LENGTH_MASK);
                deserializePacketInfo.ResponseID = 0;

                if (bytesTransferred == deserializePacketInfo.Length + Constants.UDP_HEADER_SIZE)
                {
                    int offset = 0;
                    if ((packetHeader & Constants.RESPONSE_BIT_MASK) != 0)
                    {
                        deserializePacketInfo.ResponseID = *(uint*)(src + Constants.UDP_HEADER_SIZE);
                        offset                           = 4;
                    }

                    int l = 0;
                    CompressionMode compressionMode =
                        (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK);
                    if (compressionMode != CompressionMode.None)
                    {
                        l      =  *(int*)(src + Constants.UDP_HEADER_SIZE + offset);
                        offset += 4;
                    }

                    if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                    {
                        int cl = *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 8);
                        byte[]? bdb = bigDataHandler.Receive(
                            *(int*)(src + Constants.UDP_HEADER_SIZE + offset),
                            src + Constants.UDP_HEADER_SIZE + offset + 12, deserializePacketInfo.Length - offset - 12,
                            *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 4),
                            cl);
                        deserializePacketInfo.Length = cl;
                        if (bdb != null)
                        {
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