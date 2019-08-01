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
using Exomia.Network.Native;
using K4os.Compression.LZ4;

namespace Exomia.Network.Serialization
{
    /// <content>
    ///     A TCP serialization helper class.
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
    ///     16bit   -    CHECKSUM
    /// </remarks>
    static unsafe partial class Serialization
    {
        /// <summary>
        ///     Serialize TCP.
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
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SerializeTcp(int             packetId,
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
                *(int*)(dst + Constants.TCP_HEADER_SIZE)     =  packetId;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + 4) =  chunkOffset;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + 8) =  length;
                offset                                       =  12;
            }

            if (responseID != 0)
            {
                *dst                                               |= Constants.RESPONSE_1_BIT;
                *(uint*)(dst + Constants.TCP_HEADER_SIZE + offset) =  responseID;
                offset                                             += 4;
            }

            int    l;
            ushort checksum;

            if (chunkLength >= Constants.LENGTH_THRESHOLD && compressionMode != CompressionMode.None)
            {
                byte[] buffer = ByteArrayPool.Rent(chunkLength);
                fixed (byte* bPtr = buffer)
                {
                    int s;
                    switch (compressionMode)
                    {
                        case CompressionMode.Lz4:
                            s = LZ4Codec.Encode(src, chunkLength, bPtr, chunkLength);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(compressionMode), compressionMode, "Not supported!");
                    }
                    if (s > 0 && s < chunkLength)
                    {
                        checksum = PayloadEncoding.Encode(bPtr, s, dst + Constants.TCP_HEADER_SIZE + offset + 4, out l);
                        ByteArrayPool.Return(buffer);

                        *dst |= (byte)compressionMode;
                        *(uint*)(dst + 1) =
                            ((uint)(s + offset + 5) & Constants.DATA_LENGTH_MASK) |
                            (commandID << Constants.COMMAND_ID_SHIFT);
                        *(ushort*)(dst + 5)                                       = checksum;
                        *(int*)(dst + Constants.TCP_HEADER_SIZE + offset)         = chunkLength;
                        *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + 4 + l) = Constants.ZERO_BYTE;
                        return Constants.TCP_HEADER_SIZE + offset + s + 5;
                    }
                    ByteArrayPool.Return(buffer);
                }
            }

            checksum = PayloadEncoding.Encode(src, chunkLength, dst + Constants.TCP_HEADER_SIZE + offset, out l);
            *(uint*)(dst + 1) =
                ((uint)(l + offset + 1) & Constants.DATA_LENGTH_MASK) | (commandID << Constants.COMMAND_ID_SHIFT);
            *(ushort*)(dst + 5)                                   = checksum;
            *(int*)(dst + Constants.TCP_HEADER_SIZE + offset + l) = Constants.ZERO_BYTE;

            return Constants.TCP_HEADER_SIZE + offset + l + 1;
        }

        internal static bool DeserializeTcp(CircularBuffer circularBuffer,
                                            byte[]         bufferWrite,
                                            byte[]         bufferRead,
                                            int            bytesTransferred,
                                            BigDataHandler bigDataHandler,
                                            out uint       commandID,
                                            out uint       responseID,
                                            out byte[]     data,
                                            out int        dataLength)
        {
            int size = circularBuffer.Write(bufferWrite, 0, bytesTransferred);
            while (circularBuffer.PeekHeader(
                       0, out byte packetHeader, out commandID, out dataLength, out ushort checksum)
                && dataLength <= circularBuffer.Count - Constants.TCP_HEADER_SIZE)
            {
                if (circularBuffer.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b) &&
                    b == Constants.ZERO_BYTE)
                {
                    fixed (byte* ptr = bufferRead)
                    {
                        circularBuffer.Read(ptr, dataLength, Constants.TCP_HEADER_SIZE);
                        if (size < bytesTransferred)
                        {
                            circularBuffer.Write(bufferWrite, size, bytesTransferred - size);
                        }

                        responseID = 0;
                        int packetId    = 0;
                        int chunkOffset = 0;
                        int length      = 0;
                        int offset      = 0;
                        if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                        {
                            packetId    = *(int*)(ptr + offset);
                            chunkOffset = *(int*)(ptr + offset + 4);
                            length      = *(int*)(ptr + offset + 8);

                            offset += 12;
                        }

                        if ((packetHeader & Constants.RESPONSE_BIT_MASK) != 0)
                        {
                            responseID =  *(uint*)(ptr + offset);
                            offset     += 4;
                        }

                        CompressionMode compressionMode =
                            (CompressionMode)(packetHeader & Constants.COMPRESSED_MODE_MASK);
                        if (compressionMode != CompressionMode.None)
                        {
                            offset += 4;
                        }

                        data = ByteArrayPool.Rent(dataLength);
                        if (PayloadEncoding.Decode(
                                ptr + offset, dataLength - offset - 1, data, out dataLength) == checksum)
                        {
                            switch (compressionMode)
                            {
                                case CompressionMode.None:
                                    break;
                                case CompressionMode.Lz4:
                                    int    l      = *(int*)((ptr + offset) - 4);
                                    byte[] buffer = ByteArrayPool.Rent(l);
                                    int    s      = LZ4Codec.Decode(data, 0, dataLength, buffer, 0, l);
                                    if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }

                                    ByteArrayPool.Return(data);
                                    data       = buffer;
                                    dataLength = l;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(CompressionMode), compressionMode, "Not supported!");
                            }

                            if ((packetHeader & Constants.IS_CHUNKED_1_BIT) != 0)
                            {
                                fixed (byte* dst = data)
                                {
                                    data = bigDataHandler.Receive(packetId, dst, dataLength, chunkOffset, length);
                                }
                                if (data != null)
                                {
                                    dataLength = data.Length;
                                    return true;
                                }
                                return false;
                            }

                            return true;
                        }
                        return false;
                    }
                }
                bool skipped = circularBuffer.SkipUntil(Constants.TCP_HEADER_SIZE, Constants.ZERO_BYTE);
                if (size < bytesTransferred)
                {
                    size += circularBuffer.Write(bufferWrite, size, bytesTransferred - size);
                }
                if (!skipped && !circularBuffer.SkipUntil(0, Constants.ZERO_BYTE)) { break; }
            }
            data       = null;
            responseID = 0;
            return false;
        }
    }
}