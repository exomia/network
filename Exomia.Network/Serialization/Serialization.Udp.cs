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
    /// <content>
    ///     A UDP serialization helper class.
    /// </content>
    /// <remarks>
    ///     8bit
    ///     | IS_CHUNKED BIT | RESPONSE BIT | COMPRESSED MODE | ENCRYPT MODE |
    ///     | 7              | 6            | 5  4  3         | 2  1  0      |
    ///     | VR: 0/1        | VR: 0/1      | VR: 0-8         | VR: 0-8      | VR = VALUE RANGE
    ///     -----------------------------------------------------------------------------------------------------------------------
    ///     | 0              | 0            | 0  0  0         | 1  1  1      | ENCRYPT_MODE_MASK    0b00000111
    ///     | 0              | 0            | 1  1  1         | 0  0  0      | COMPRESSED_MODE_MASK 0b00111000
    ///     | 0              | 1            | 0  0  0         | 0  0  0      | RESPONSE_BIT_MASK    0b01000000
    ///     | 1              | 0            | 0  0  0         | 0  0  0      | IS_CHUNKED_BIT_MASK  0b10000000
    ///     32bit
    ///     | COMMANDID 31-16 (16)bit                          | DATA LENGTH 15-0 (16)bit                        |
    ///     | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
    ///     | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE
    ///     RANGE
    ///     --------------------------------------------------------------------------------------------------------------------------------
    ///     |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |
    ///     DATA_LENGTH_MASK 0xFFFF
    ///     |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |
    ///     COMMANDID_MASK 0xFFFF0000
    /// </remarks>
    static unsafe partial class Serialization
    {
        /// <summary>
        ///     Serialize UDP.
        /// </summary>
        /// <param name="packetId">        Identifier for the packet. </param>
        /// <param name="commandID">       Identifier for the command. </param>
        /// <param name="responseID">      Identifier for the response. </param>
        /// <param name="src">             [in,out] If non-null, source for the. </param>
        /// <param name="dst">             [in,out] If non-null, destination for the. </param>
        /// <param name="chunkLength">     Length of the chunk. </param>
        /// <param name="chunkOffset">     The chunk offset. </param>
        /// <param name="length">          The length. </param>
        /// <param name="encryptionMode">  The encryption mode. </param>
        /// <param name="compressionMode"> The compression mode. </param>
        /// <returns>
        ///     An int.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when one or more arguments are outside the required range. </exception>
        internal static int SerializeUdp(int             packetId,
                                         uint            commandID,
                                         uint            responseID,
                                         byte*           src,
                                         byte*           dst,
                                         int             chunkLength,
                                         int             chunkOffset,
                                         int             length,
                                         EncryptionMode  encryptionMode,
                                         CompressionMode compressionMode)
        {
            *dst = (byte)encryptionMode;

            int offset = 0;
            if (chunkLength != length)
            {
                *dst                                         |= Constants.IS_CHUNKED_1_BIT;
                *(int*)(dst + Constants.UDP_HEADER_SIZE)     =  packetId;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + 4) =  chunkOffset;
                *(int*)(dst + Constants.UDP_HEADER_SIZE + 8) =  length;
                offset                                       =  12;
            }

            if (responseID != 0)
            {
                *dst                                               |= Constants.RESPONSE_1_BIT;
                *(uint*)(dst + Constants.UDP_HEADER_SIZE + offset) =  responseID;
                offset                                             += 4;
            }

            if (length >= Constants.LENGTH_THRESHOLD && compressionMode != CompressionMode.None)
            {
                int s;
                switch (compressionMode)
                {
                    case CompressionMode.Lz4:
                        s = LZ4Codec.Encode(src, length, dst + Constants.UDP_HEADER_SIZE + offset + 4, length);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(compressionMode), compressionMode, "Not supported!");
                }
                if (s > 0 && s < length)
                {
                    *dst |= (byte)compressionMode;
                    *(uint*)(dst + 1) =
                        ((uint)(s + offset + 4) & Constants.DATA_LENGTH_MASK) |
                        (commandID << Constants.COMMAND_ID_SHIFT);
                    *(int*)(dst + Constants.UDP_HEADER_SIZE + offset) = length;
                    return Constants.UDP_HEADER_SIZE + offset + 4 + s;
                }
            }

            *(uint*)(dst + 1) =
                ((uint)(length + offset) & Constants.DATA_LENGTH_MASK) | (commandID << Constants.COMMAND_ID_SHIFT);
            Mem.Cpy(dst + Constants.UDP_HEADER_SIZE + offset, src, length);
            return Constants.UDP_HEADER_SIZE + offset + length;
        }

        internal static bool DeserializeUdp(byte[]         buffer,
                                            int            bytesTransferred,
                                            BigDataHandler bigDataHandler,
                                            out uint       commandID,
                                            out uint       responseID,
                                            out byte[]     data,
                                            out int        dataLength)
        {
            fixed (byte* src = buffer)
            {
                byte packetHeader = *src;
                uint h2           = *(uint*)(src + 1);
                commandID  = h2 >> Constants.COMMAND_ID_SHIFT;
                dataLength = (int)(h2 & Constants.DATA_LENGTH_MASK);
                responseID = 0;

                if (bytesTransferred == dataLength + Constants.UDP_HEADER_SIZE)
                {
                    int offset = 0;
                    if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                    {
                        offset += 12;
                    }

                    if ((packetHeader & Constants.RESPONSE_BIT_MASK) != 0)
                    {
                        responseID =  *(uint*)(src + Constants.UDP_HEADER_SIZE + offset);
                        offset     += 4;
                    }

                    switch ((CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK))
                    {
                        case CompressionMode.Lz4:
                            int l = *(int*)(src + Constants.UDP_HEADER_SIZE + offset);
                            offset -= 4;
                            fixed (byte* dst = data = ByteArrayPool.Rent(l))
                            {
                                int s = LZ4Codec.Decode(
                                    src + Constants.UDP_HEADER_SIZE + offset, dataLength - offset, dst, l);
                                if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                                {
                                    data = bigDataHandler.Receive(
                                        *(int*)(src + Constants.UDP_HEADER_SIZE + offset), dst, l,
                                        *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 4),
                                        *(int*)(src + offset + 8));
                                    if (data != null)
                                    {
                                        dataLength = data.Length;
                                        return true;
                                    }
                                    return false;
                                }
                            }
                            dataLength = l;
                            return true;
                        case CompressionMode.None:
                            dataLength -= offset;

                            if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                            {
                                data = bigDataHandler.Receive(
                                    *(int*)(src + Constants.UDP_HEADER_SIZE + offset),
                                    src + Constants.UDP_HEADER_SIZE + offset, dataLength,
                                    *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 4),
                                    *(int*)(src + Constants.UDP_HEADER_SIZE + offset + 8));
                                if (data != null)
                                {
                                    dataLength = data.Length;
                                    return true;
                                }
                                return false;
                            }

                            fixed (byte* dst = data = ByteArrayPool.Rent(dataLength))
                            {
                                Mem.Cpy(dst, src + Constants.UDP_HEADER_SIZE + offset, dataLength);
                            }

                            return true;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(CompressionMode),
                                (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK),
                                "Not supported!");
                    }
                }
            }
            data = null;
            return false;
        }
    }
}