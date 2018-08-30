#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System;
using System.Runtime.CompilerServices;
using Exomia.Network.Buffers;
using LZ4;

namespace Exomia.Network.Serialization
{
    static unsafe partial class Serialization
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeUdp(uint commandID, byte[] data, int offset, int length, uint responseID,
            EncryptionMode encryptionMode, out byte[] send, out int size)
        {
            send = ByteArrayPool.Rent(Constants.UDP_HEADER_SIZE + 8 + length);
            SerializeUdp(commandID, data, offset, length, responseID, encryptionMode, send, out size);
        }

        internal static void SerializeUdp(uint commandID, byte[] data, int offset, int length, uint responseID,
            EncryptionMode encryptionMode, byte[] send, out int size)
        {
            // 8bit
            // 
            // | UNUSED BIT   | RESPONSE BIT | COMPRESSED BIT | ENCRYPT BIT | ENCRYPT MODE |
            // | 7            | 6            | 5              | 4           | 3  2  1  0   |
            // | VR: 0/1      | VR: 0/1      | VR: 0/1        | VR: 0/1     | VR: 0-15     | VR = VALUE RANGE
            // -------------------------------------------------------------------------------------------------------------
            // | 0            | 0            | 0              | 0           | 1  1  1  1   | ENCRYPT_MODE_MASK    0b00001111
            // | 0            | 0            | 0              | 1           | 0  0  0  0   | ENCRYPT_BIT_MASK     0b00010000
            // | 0            | 0            | 1              | 0           | 0  0  0  0   | COMPRESSED_BIT_MASK  0b00100000
            // | 0            | 1            | 0              | 0           | 0  0  0  0   | RESPONSE_BIT_MASK    0b01000000
            // | 1            | 0            | 0              | 0           | 0  0  0  0   | UNUSED_BIT_MASK      0b10000000

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
                            *ptr = (byte)(RESPONSE_1_BIT | COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 8) & DATA_LENGTH_MASK) |
                                (commandID << COMMANDID_SHIFT);
                            *(uint*)(ptr + 5) = responseID;
                            *(int*)(ptr + 9) = length;
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
                        (commandID << COMMANDID_SHIFT);
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
                            *ptr = (byte)(COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 4) & DATA_LENGTH_MASK) |
                                (commandID << COMMANDID_SHIFT);
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
                        (commandID << COMMANDID_SHIFT);
                }
                Buffer.BlockCopy(data, offset, send, Constants.UDP_HEADER_SIZE, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeaderUdp(this byte[] header, out byte packetHeader, out uint commandID,
            out int dataLength)
        {
            // 8bit
            // 
            // | UNUSED BIT   | RESPONSE BIT | COMPRESSED BIT | ENCRYPT BIT | ENCRYPT MODE |
            // | 7            | 6            | 5              | 4           | 3  2  1  0   |
            // | VR: 0/1      | VR: 0/1      | VR: 0/1        | VR: 0/1     | VR: 0-15     | VR = VALUE RANGE
            // -------------------------------------------------------------------------------------------------------------
            // | 0            | 0            | 0              | 0           | 1  1  1  1   | ENCRYPT_MODE_MASK    0b00001111
            // | 0            | 0            | 0              | 1           | 0  0  0  0   | ENCRYPT_BIT_MASK     0b00010000
            // | 0            | 0            | 1              | 0           | 0  0  0  0   | COMPRESSED_BIT_MASK  0b00100000
            // | 0            | 1            | 0              | 0           | 0  0  0  0   | RESPONSE_BIT_MASK    0b01000000
            // | 1            | 0            | 0              | 0           | 0  0  0  0   | UNUSED_BIT_MASK      0b10000000

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
                int h2 = *(int*)(ptr + 1);
                commandID = (uint)(h2 >> COMMANDID_SHIFT);
                dataLength = h2 & DATA_LENGTH_MASK;
            }
        }
    }
}