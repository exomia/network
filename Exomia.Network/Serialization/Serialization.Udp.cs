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
using LZ4;

namespace Exomia.Network.Serialization
{
    /// <content>
    ///     A serialization.
    /// </content>
    static unsafe partial class Serialization
    {
        /// <summary>
        ///     Serialize UDP.
        /// </summary>
        /// <param name="commandID">      [out] Identifier for the command. </param>
        /// <param name="data">           The data. </param>
        /// <param name="offset">         The offset. </param>
        /// <param name="length">         The length. </param>
        /// <param name="responseID">     Identifier for the response. </param>
        /// <param name="encryptionMode"> The encryption mode. </param>
        /// <param name="send">           [out] The send. </param>
        /// <param name="size">           [out] The size. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeUdp(uint           commandID, byte[] data, int offset, int length,
                                          uint           responseID,
                                          EncryptionMode encryptionMode, out byte[] send, out int size)
        {
            send = ByteArrayPool.Rent(Constants.UDP_HEADER_SIZE + 8 + length);
            SerializeUdp(commandID, data, offset, length, responseID, encryptionMode, send, out size);
        }

        /// <summary>
        ///     Serialize UDP.
        /// </summary>
        /// <param name="commandID">      [out] Identifier for the command. </param>
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
        internal static void SerializeUdp(uint           commandID, byte[] data, int offset, int length,
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

            if (responseID != 0)
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    int s = LZ4Codec.Encode(
                        data, offset, length, send, Constants.UDP_HEADER_SIZE + 8, length);

                    if (s > Constants.UDP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.UDP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }

                    if (s > 0)
                    {
                        size = Constants.UDP_HEADER_SIZE + 8 + s;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(RESPONSE_1_BIT | (byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 8) & DATA_LENGTH_MASK) |
                                (commandID << COMMAND_ID_SHIFT);
                            *(uint*)(ptr + 5) = responseID;
                            *(int*)(ptr  + 9) = length;
                        }
                        return;
                    }
                }

                size = Constants.UDP_HEADER_SIZE + 4 + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                    *(uint*)(ptr + 1) =
                        ((uint)(length + 4) & DATA_LENGTH_MASK) |
                        (commandID << COMMAND_ID_SHIFT);
                    *(uint*)(ptr + 5) = responseID;
                }
                Buffer.BlockCopy(data, offset, send, Constants.UDP_HEADER_SIZE + 4, length);
            }
            else
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    int s = LZ4Codec.Encode(
                        data, offset, length, send, Constants.UDP_HEADER_SIZE + 4, length);
                    if (s > Constants.UDP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.UDP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }
                    if (s > 0)
                    {
                        size = Constants.UDP_HEADER_SIZE + 4 + s;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)((byte)CompressionMode.Lz4 | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 4) & DATA_LENGTH_MASK) |
                                (commandID << COMMAND_ID_SHIFT);
                            *(int*)(ptr + 5) = length;
                        }
                        return;
                    }
                }

                size = Constants.UDP_HEADER_SIZE + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)encryptionMode;
                    *(uint*)(ptr + 1) =
                        ((uint)length & DATA_LENGTH_MASK) |
                        (commandID << COMMAND_ID_SHIFT);
                }
                Buffer.BlockCopy(data, offset, send, Constants.UDP_HEADER_SIZE, length);
            }
        }

        /// <summary>
        ///     A byte[] extension method that gets header UDP.
        /// </summary>
        /// <param name="header">       The header to act on. </param>
        /// <param name="packetHeader"> [out] The packet header. </param>
        /// <param name="commandID">    [out] Identifier for the command. </param>
        /// <param name="dataLength">   [out] Length of the data. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeaderUdp(this byte[] header, out byte packetHeader, out uint commandID,
                                          out  int    dataLength)
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

            fixed (byte* ptr = header)
            {
                packetHeader = *ptr;
                uint h2 = *(uint*)(ptr + 1);
                commandID  = h2 >> COMMAND_ID_SHIFT;
                dataLength = (int)(h2 & DATA_LENGTH_MASK);
            }
        }
    }
}