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

namespace Exomia.Network.Serialization
{
    internal static unsafe class Serialization
    {
        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialize(uint commandID, byte[] data, int offset, int lenght, uint responseID,
            out byte[] send, out int size)
        {
            // 32bit
            // 
            // | COMMANDID 0-15 (16)bit                         | RESPONSE BIT | UNUSED2 BIT  | DATALENGTH 18-31 (14)bit                  |
            // | 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 | 16           | 17           | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-65535                                    | VR: 0/1      | VR: 0/1      | VR: 0-16382                               | VR = VALUE RANGE
            // 
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0           |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | UNUSED1_BIT_MASK 0x4000
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | RESPONSE_BIT_MASK 0x8000
            // | 1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000
            if (responseID != 0)
            {
                size = Constants.HEADER_SIZE + Constants.RESPONSE_SIZE + lenght;
                send = ByteArrayPool.Rent(size);
                fixed (byte* ptr = send)
                {
                    *(uint*)ptr =
                        ((uint)(lenght + Constants.RESPONSE_SIZE) & Constants.DATA_LENGTH_MASK) |
                        (0u << 14) |
                        (1u << 15) |
                        (commandID << 16);
                    *(uint*)(ptr + 4) = responseID;
                }

                //DATA
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE + Constants.RESPONSE_SIZE, lenght);
            }
            else
            {
                size = Constants.HEADER_SIZE + lenght;
                send = ByteArrayPool.Rent(size);
                fixed (byte* ptr = send)
                {
                    *(uint*)ptr =
                        ((uint)lenght & Constants.DATA_LENGTH_MASK) |
                        (0u << 14) |
                        (0u << 15) |
                        (commandID << 16);
                }

                //DATA
                Buffer.BlockCopy(data, offset, send, Constants.HEADER_SIZE, lenght);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out int dataLenght, out uint response)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-15 (16)bit                         | RESPONSE BIT | UNUSED BIT   | DATALENGTH 18-31 (14)bit                  |
            // | 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 | 16           | 17           | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-65535                                    | VR: 0/1      | VR: 0/1      | VR: 0-16382                               | VR = VALUE RANGE
            // 
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0           |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | UNUSED1_BIT_MASK 0x4000
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | RESPONSE_BIT_MASK 0x8000
            // | 1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

            fixed (byte* ptr = header)
            {
                uint h = *(uint*)ptr;
                commandID = (h & Constants.COMMANDID_MASK) >> 16;

                response = (h & Constants.RESPONSE_BIT_MASK) >> 15;

                //uint unused1 = (h & Constants.UNUSED1BIT_MASK ) >> 14;
                dataLenght = (int)(h & Constants.DATA_LENGTH_MASK);
            }
        }

        #endregion
    }
}