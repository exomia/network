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
using Exomia.Network.Lib;
using K4os.Compression.LZ4;

namespace Exomia.Network.Serialization
{
    /// <content>
    ///     A serialization.
    /// </content>
    static unsafe partial class Serialization
    {
        /// <summary>
        ///     Serialize TCP.
        /// </summary>
        /// <param name="commandID">      Identifier for the command. </param>
        /// <param name="src">            [in,out] If non-null, source for the. </param>
        /// <param name="length">         The length. </param>
        /// <param name="responseID">     Identifier for the response. </param>
        /// <param name="encryptionMode"> The encryption mode. </param>
        /// <param name="dst">            [in,out] If non-null, destination for the. </param>
        /// <param name="size">           [out] The size. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint           commandID,
                                          byte*          src,
                                          int            length,
                                          uint           responseID,
                                          EncryptionMode encryptionMode,
                                          out byte[]     dst,
                                          out int        size)
        {
            dst = ByteArrayPool.Rent(Constants.TCP_HEADER_SIZE + 9 + length + Math2.Ceiling(length / 7.0f));
            fixed (byte* ptr = dst)
            {
                SerializeTcp(commandID, src, length, responseID, encryptionMode, ptr, out size);
            }
        }

        /// <summary>
        ///     Serialize TCP.
        /// </summary>
        /// <param name="commandID">      Identifier for the command. </param>
        /// <param name="src">            [in,out] If non-null, source for the. </param>
        /// <param name="length">         The length. </param>
        /// <param name="responseID">     Identifier for the response. </param>
        /// <param name="encryptionMode"> The encryption mode. </param>
        /// <param name="dst">            [in,out] If non-null, destination for the. </param>
        /// <param name="size">           [out] The size. </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint           commandID,
                                          byte*          src,
                                          int            length,
                                          uint           responseID,
                                          EncryptionMode encryptionMode,
                                          byte*          dst,
                                          out int        size)
        {
            // 8bit
            // 
            // | UNUSED BIT   | RESPONSE BIT | COMPRESSED MODE | ENCRYPT MODE |
            // | 7            | 6            | 5  4  3         | 2  1  0      |
            // | VR: 0/1      | VR: 0/1      | VR: 0-8         | VR: 0-8      | VR = VALUE RANGE
            // ---------------------------------------------------------------------------------------------------------------------
            // | 0            | 0            | 0  0  0         | 1  1  1      | ENCRYPT_MODE_MASK    0b00000111
            // | 0            | 0            | 1  1  1         | 0  0  0      | COMPRESSED_MODE_MASK 0b00111000
            // | 0            | 1            | 0  0  0         | 0  0  0      | RESPONSE_BIT_MASK    0b01000000
            // | 1            | 0            | 0  0  0         | 0  0  0      | UNUSED_BIT_MASK      0b10000000

            // 32bit
            // 
            // | COMMANDID 31-16 (16)bit                          | DATA LENGTH 15-0 (16)bit                        |
            // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
            // | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE RANGE
            // --------------------------------------------------------------------------------------------------------------------------------
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
            // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

            // 16bit   -    CHECKSUM

            int    l;
            ushort checksum;

            if (responseID != 0)
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    byte[] buffer = ByteArrayPool.Rent(length);
                    fixed (byte* bPtr = buffer)
                    {
                        int s = LZ4Codec.Encode(src, length, bPtr, length);
                        if (s > Constants.TCP_PACKET_SIZE_MAX)
                        {
                            ByteArrayPool.Return(buffer);
                            throw new ArgumentOutOfRangeException(
                                $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                        }
                        if (s > 0)
                        {
                            checksum = PayloadEncoding.Encode(bPtr, s, dst + Constants.TCP_HEADER_SIZE + 8, out l);
                            ByteArrayPool.Return(buffer);

                            size = Constants.TCP_HEADER_SIZE + 9 + l;

                            *dst = (byte)(RESPONSE_1_BIT | (byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(dst + 1) =
                                ((uint)(l + 9) & DATA_LENGTH_MASK)
                              | (commandID << COMMAND_ID_SHIFT);
                            *(ushort*)(dst + 5)                              = checksum;
                            *(uint*)(dst + 7)                                = responseID;
                            *(int*)(dst + 11)                                = length;
                            *(int*)(dst + Constants.TCP_HEADER_SIZE + l + 8) = Constants.ZERO_BYTE;

                            return;
                        }
                        ByteArrayPool.Return(buffer);
                    }
                }

                checksum = PayloadEncoding.Encode(src, length, dst + Constants.TCP_HEADER_SIZE + 4, out l);
                size     = Constants.TCP_HEADER_SIZE + 5 + l;

                *dst = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                *(uint*)(dst + 1) =
                    ((uint)(l + 5) & DATA_LENGTH_MASK)
                  | (commandID << COMMAND_ID_SHIFT);
                *(ushort*)(dst + 5)                              = checksum;
                *(uint*)(dst + 7)                                = responseID;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + l + 4) = Constants.ZERO_BYTE;
            }
            else
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    byte[] buffer = ByteArrayPool.Rent(length);
                    fixed (byte* bPtr = buffer)
                    {
                        int s = LZ4Codec.Encode(src, length, bPtr, length);
                        if (s > Constants.TCP_PACKET_SIZE_MAX)
                        {
                            ByteArrayPool.Return(buffer);
                            throw new ArgumentOutOfRangeException(
                                $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                        }
                        if (s > 0)
                        {
                            checksum = PayloadEncoding.Encode(bPtr, s, dst + Constants.TCP_HEADER_SIZE + 4, out l);
                            ByteArrayPool.Return(buffer);

                            size = Constants.TCP_HEADER_SIZE + 5 + l;

                            *dst = (byte)((byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(dst + 1) =
                                ((uint)(l + 5) & DATA_LENGTH_MASK)
                              | (commandID << COMMAND_ID_SHIFT);
                            *(ushort*)(dst + 5)                              = checksum;
                            *(int*)(dst + 7)                                 = length;
                            *(int*)(dst + Constants.TCP_HEADER_SIZE + l + 4) = Constants.ZERO_BYTE;

                            return;
                        }
                        ByteArrayPool.Return(buffer);
                    }
                }

                checksum = PayloadEncoding.Encode(src, length, dst + Constants.TCP_HEADER_SIZE, out l);
                size     = Constants.TCP_HEADER_SIZE + 1 + l;

                *dst = (byte)encryptionMode;
                *(uint*)(dst + 1) =
                    ((uint)(l + 1) & DATA_LENGTH_MASK)
                  | (commandID << COMMAND_ID_SHIFT);
                *(ushort*)(dst + 5)                          = checksum;
                *(int*)(dst + Constants.TCP_HEADER_SIZE + l) = Constants.ZERO_BYTE;
            }
        }
    }
}