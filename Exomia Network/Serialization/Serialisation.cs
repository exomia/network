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

        private const uint COMMANDID_MASK = 0xFFFC0000;
        private const int COMMANDID_SHIFT = 18;

        private const uint RESPONSE_BIT_MASK = 0x20000;
        private const int RESPONSE_SHIFT = 17;

        private const uint COMPRESSED_BIT_MASK = 0x10000;
        private const int COMPRESSED_SHIFT = 16;

        private const uint DATA_LENGTH_MASK = 0xFFFF;

        private const uint RESPONSE_1_BIT = 1u << RESPONSE_SHIFT;
        private const uint COMPRESSED_1_BIT = 1u << COMPRESSED_SHIFT;

        private const int LENGTH_THRESHOLD = 1 << 12; //4096

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialize(uint commandID, byte[] data, int offset, int length, uint responseID,
            out byte[] send, out int size)
        {
            // 32bit
            // 
            // | COMMANDID 0-15 (14)bit                    | RESPONSE BIT | COMPRESSED BIT | DATALENGTH 18-31 (16)bit                        |
            // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 | 17           | 16             | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
            // | VR: 0-16383                               | VR: 0/1      | VR: 0/1        | VR: 0-65535                                     | VR = VALUE RANGE
            // 
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0             |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMPRESSED_BIT_MASK 0x10000
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | RESPONSE_BIT_MASK 0x20000
            // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFC0000
            //COMPRESS DATA
            if (length >= LENGTH_THRESHOLD)
            {
                if (responseID != 0)
                {
                    send = ByteArrayPool.Rent(Constants.HEADER_SIZE + 8 + length);
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
                            *(uint*)ptr =
                                ((uint)(s + 8) & DATA_LENGTH_MASK) |
                                COMPRESSED_1_BIT |
                                RESPONSE_1_BIT |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                            *(uint*)(ptr + 4) = responseID;
                            *(int*)(ptr + 8) = length;
                        }
                    }
                    else
                    {
                        size = Constants.HEADER_SIZE + 4 + length;
                        fixed (byte* ptr = send)
                        {
                            *(uint*)ptr =
                                ((uint)(length + 4) & DATA_LENGTH_MASK) |
                                RESPONSE_1_BIT |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                            *(uint*)(ptr + 4) = responseID;
                        }
                        Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE + 4, length);
                    }
                }
                else
                {
                    send = ByteArrayPool.Rent(Constants.HEADER_SIZE + 4 + length);
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
                            *(uint*)ptr =
                                ((uint)(s + 4) & DATA_LENGTH_MASK) |
                                COMPRESSED_1_BIT |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                            *(int*)(ptr + 4) = length;
                        }
                    }
                    else
                    {
                        size = Constants.HEADER_SIZE + length;
                        fixed (byte* ptr = send)
                        {
                            *(uint*)ptr =
                                ((uint)length & DATA_LENGTH_MASK) |
                                ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                        }
                        Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE, length);
                    }
                }
            }
            else if (responseID != 0)
            {
                size = Constants.HEADER_SIZE + 4 + length;
                send = ByteArrayPool.Rent(size);
                fixed (byte* ptr = send)
                {
                    *(uint*)ptr =
                        ((uint)(length + 4) & DATA_LENGTH_MASK) |
                        RESPONSE_1_BIT |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                    *(uint*)(ptr + 4) = responseID;
                }
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE + 4, length);
            }
            else
            {
                size = Constants.HEADER_SIZE + length;
                send = ByteArrayPool.Rent(size);
                fixed (byte* ptr = send)
                {
                    *(uint*)ptr =
                        ((uint)length & DATA_LENGTH_MASK) |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                }
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out int dataLenght, out uint response,
            out uint compressed)
        {
            // 32bit
            // 
            // | COMMANDID 0-15 (14)bit                    | RESPONSE BIT | COMPRESSED BIT | DATALENGTH 18-31 (16)bit                        |
            // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 | 17           | 16             | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
            // | VR: 0-16383                               | VR: 0/1      | VR: 0/1        | VR: 0-65535                                     | VR = VALUE RANGE
            // 
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0             |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMPRESSED_BIT_MASK 0x10000
            // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | RESPONSE_BIT_MASK 0x20000
            // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0             |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFC0000

            fixed (byte* ptr = header)
            {
                uint h = *(uint*)ptr;
                commandID = (h & COMMANDID_MASK) >> COMMANDID_SHIFT;
                response = (h & RESPONSE_BIT_MASK) >> RESPONSE_SHIFT;
                compressed = (h & COMPRESSED_BIT_MASK) >> COMPRESSED_SHIFT;
                dataLenght = (int)(h & DATA_LENGTH_MASK);
            }
        }

        #endregion
    }
}