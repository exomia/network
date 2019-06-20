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
using LZ4;

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
        /// <param name="data">           The data. </param>
        /// <param name="offset">         The offset. </param>
        /// <param name="length">         The length. </param>
        /// <param name="responseID">     Identifier for the response. </param>
        /// <param name="encryptionMode"> The encryption mode. </param>
        /// <param name="send">           [out] The send. </param>
        /// <param name="size">           [out] The size. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint           commandID, byte[] data, int offset, int length,
                                          uint           responseID,
                                          EncryptionMode encryptionMode, out byte[] send, out int size)
        {
            send = ByteArrayPool.Rent(Constants.TCP_HEADER_SIZE + 9 + length + Math2.Ceiling(length / 7.0f));
            SerializeTcp(commandID, data, offset, length, responseID, encryptionMode, send, out size);
        }

        /// <summary>
        ///     Serialize TCP.
        /// </summary>
        /// <param name="commandID">      Identifier for the command. </param>
        /// <param name="data">           The data. </param>
        /// <param name="offset">         The offset. </param>
        /// <param name="length">         The length. </param>
        /// <param name="responseID">     Identifier for the response. </param>
        /// <param name="encryptionMode"> The encryption mode. </param>
        /// <param name="send">           The send. </param>
        /// <param name="size">           [out] The size. </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint           commandID, byte[] data, int offset, int length,
                                          uint           responseID,
                                          EncryptionMode encryptionMode, byte[] send, out int size)
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
                    int s = LZ4Codec.Encode(
                        data, offset, length, buffer, 0, length);
                    if (s > Constants.TCP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }
                    if (s > 0)
                    {
                        checksum = PayloadEncoding.Encode(
                            buffer, 0, s, send, Constants.TCP_HEADER_SIZE + 8, out l);
                        size = Constants.TCP_HEADER_SIZE + 9 + l;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(RESPONSE_1_BIT | (byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(l + 9) & DATA_LENGTH_MASK)
                              | (commandID << COMMAND_ID_SHIFT);
                            *(ushort*)(ptr                              + 5)  = checksum;
                            *(uint*)(ptr                                + 7)  = responseID;
                            *(int*)(ptr                                 + 11) = length;
                            *(int*)(ptr + Constants.TCP_HEADER_SIZE + l + 8)  = Constants.ZERO_BYTE;
                        }
                        return;
                    }
                }

                checksum = PayloadEncoding.Encode(
                    data, offset, length, send, Constants.TCP_HEADER_SIZE + 4, out l);
                size = Constants.TCP_HEADER_SIZE + 5 + l;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                    *(uint*)(ptr + 1) =
                        ((uint)(l + 5) & DATA_LENGTH_MASK)
                      | (commandID << COMMAND_ID_SHIFT);
                    *(ushort*)(ptr                              + 5) = checksum;
                    *(uint*)(ptr                                + 7) = responseID;
                    *(int*)(ptr + Constants.TCP_HEADER_SIZE + l + 4) = Constants.ZERO_BYTE;
                }
            }
            else
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    byte[] buffer = ByteArrayPool.Rent(length);
                    int s = LZ4Codec.Encode(
                        data, offset, length, buffer, 0, length);
                    if (s > Constants.TCP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }
                    if (s > 0)
                    {
                        checksum = PayloadEncoding.Encode(buffer, 0, s, send, Constants.TCP_HEADER_SIZE + 4, out l);
                        size     = Constants.TCP_HEADER_SIZE + 5 + l;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)((byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(l + 5) & DATA_LENGTH_MASK)
                              | (commandID << COMMAND_ID_SHIFT);
                            *(ushort*)(ptr                              + 5) = checksum;
                            *(int*)(ptr                                 + 7) = length;
                            *(int*)(ptr + Constants.TCP_HEADER_SIZE + l + 4) = Constants.ZERO_BYTE;
                        }
                        return;
                    }
                }

                checksum = PayloadEncoding.Encode(
                    data, offset, length, send, Constants.TCP_HEADER_SIZE, out l);
                size = Constants.TCP_HEADER_SIZE + 1 + l;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)encryptionMode;
                    *(uint*)(ptr + 1) =
                        ((uint)(l + 1) & DATA_LENGTH_MASK)
                      | (commandID << COMMAND_ID_SHIFT);
                    *(ushort*)(ptr                          + 5) = checksum;
                    *(int*)(ptr + Constants.TCP_HEADER_SIZE + l) = Constants.ZERO_BYTE;
                }
            }
        }
    }
}