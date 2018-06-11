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
    internal static unsafe class Serialization
    {
        #region Variables

        internal const uint UNUSED_BIT_MASK = 0b10000000;
        private const byte UNUSED_1_BIT = 1 << 7;

        internal const uint RESPONSE_BIT_MASK = 0b01000000;
        private const byte RESPONSE_1_BIT = 1 << 6;

        internal const uint COMPRESSED_BIT_MASK = 0b00100000;
        private const byte COMPRESSED_1_BIT = 1 << 5;

        internal const uint ENCRYPT_BIT_MASK = 0b00010000;
        internal const uint ENCRYPT_MODE_MASK = 0b00001111;

        private const uint COMMANDID_MASK = 0xFFFF0000;
        private const int COMMANDID_SHIFT = 16;
        private const uint DATA_LENGTH_MASK = 0xFFFF;

        private const int LENGTH_THRESHOLD = 1 << 12; //4096

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialize(uint commandID, byte[] data, int offset, int length, uint responseID,
            EncryptionMode encryptionMode,
            out byte[] send, out int size)
        {
            // 8bit
            // 
            // | UNUSED BIT   | RESPONSE BIT | COMPRESSED BIT | ENCRYPT BIT | ENCRYPT MODE      |
            // | 7            | 6            | 5              | 4           | 3  2  1  0        |
            // | VR: 0/1      | VR: 0/1      | VR: 0/1        |             |                   | VR = VALUE RANGE
            // 
            // | 0            | 0            | 0              | 0           | 1  1  1  1        | ENCRYPT_MODE_MASK    0b00001111
            // | 0            | 0            | 0              | 1           | 0  0  0  0        | ENCRYPT_BIT_MASK     0b00010000
            // | 0            | 0            | 1              | 0           | 0  0  0  0        | COMPRESSED_BIT_MASK  0b00100000
            // | 0            | 1            | 0              | 0           | 0  0  0  0        | RESPONSE_BIT_MASK    0b01000000
            // | 1            | 0            | 0              | 0           | 0  0  0  0        | UNUSED_BIT_MASK      0b10000000

            // 32bit
            // 
            // | COMMANDID 0-15 (14)bit                           | DATALENGTH 18-31 (16)bit                        |
            // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
            // | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE RANGE
            // 
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
            // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

            send = ByteArrayPool.Rent(Constants.HEADER_SIZE + 8 + length);
            if (responseID != 0)
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    int s = LZ4Codec.Encode(
                        data, offset, length, send, Constants.HEADER_SIZE + 8, length);

                    if (s > Constants.PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.PACKET_SIZE_MAX} exceeded (s: {s})");
                    }

                    if (s > 0)
                    {
                        size = Constants.HEADER_SIZE + 8 + s;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(RESPONSE_1_BIT | COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 8) & DATA_LENGTH_MASK) |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                            *(uint*)(ptr + 5) = responseID;
                            *(int*)(ptr + 9) = length;
                        }
                        return;
                    }
                }

                size = Constants.HEADER_SIZE + 4 + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                    *(uint*)(ptr + 1) =
                        ((uint)(length + 4) & DATA_LENGTH_MASK) |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                    *(uint*)(ptr + 5) = responseID;
                }
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE + 4, length);
            }
            else
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    int s = LZ4Codec.Encode(
                        data, offset, length, send, Constants.HEADER_SIZE + 4, length);
                    if (s > Constants.PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.PACKET_SIZE_MAX} exceeded (s: {s})");
                    }
                    if (s > 0)
                    {
                        size = Constants.HEADER_SIZE + 4 + s;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(s + 4) & DATA_LENGTH_MASK) |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                            *(int*)(ptr + 5) = length;
                        }
                        return;
                    }
                }

                size = Constants.HEADER_SIZE + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)encryptionMode;
                    *(uint*)(ptr + 1) =
                        ((uint)length & DATA_LENGTH_MASK) |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                }
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out int dataLenght, out byte h1)
        {
            // 8bit
            // 
            // | UNUSED BIT   | RESPONSE BIT | COMPRESSED BIT | ENCRYPT BIT | ENCRYPT MODE      |
            // | 7            | 6            | 5              | 4           | 3  2  1  0        |
            // | VR: 0/1      | VR: 0/1      | VR: 0/1        |             |                   | VR = VALUE RANGE
            // 
            // | 0            | 0            | 0              | 0           | 1  1  1  1        | ENCRYPT_MODE_MASK    0b00001111
            // | 0            | 0            | 0              | 1           | 0  0  0  0        | ENCRYPT_BIT_MASK     0b00010000
            // | 0            | 0            | 1              | 0           | 0  0  0  0        | COMPRESSED_BIT_MASK  0b00100000
            // | 0            | 1            | 0              | 0           | 0  0  0  0        | RESPONSE_BIT_MASK    0b01000000
            // | 1            | 0            | 0              | 0           | 0  0  0  0        | UNUSED_BIT_MASK      0b10000000

            // 32bit
            // 
            // | COMMANDID 0-15 (14)bit                           | DATALENGTH 18-31 (16)bit                        |
            // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
            // | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE RANGE
            // 
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
            // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

            fixed (byte* ptr = header)
            {
                h1 = *ptr;
                uint h2 = *(uint*)(ptr + 1);
                commandID = (h2 & COMMANDID_MASK) >> COMMANDID_SHIFT;
                dataLenght = (int)(h2 & DATA_LENGTH_MASK);
            }
        }

        #endregion
    }
}