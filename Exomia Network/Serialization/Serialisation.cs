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
        internal static byte[] Serialize(uint commandID, byte[] data, int offset, int lenght, uint responseID)
        {
            // 32bit
            // 
            // | COMMANDID 0-15 (16)bit                         | UNUSED1 BIT  | UNUSED2 BIT  | DATALENGTH 18-31 (14)bit                  |
            // | 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 | 16           | 17           | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-65535                                    | VR: 0/1      | VR: 0/1      | VR: 0-16382                               | VR = VALUE RANGE
            // 
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0           |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | UNUSED1BIT_MASK 0x4000
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | UNUSED2BIT_MASK 0x8000
            // | 1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000
            byte[] buffer = ByteArrayPool.Rent(Constants.HEADER_SIZE + lenght);
            fixed (byte* ptr = buffer)
            {
                *(uint*)ptr =
                    ((uint)lenght & Constants.DATA_LENGTH_MASK) |

                    //(0u << 14) |
                    //(0u << 15) | 
                    (commandID << 16);
                *(uint*)(ptr + 4) = responseID;
            }

            //DATA
            Buffer.BlockCopy(data, offset, buffer, Constants.HEADER_SIZE, lenght);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out int dataLenght, out uint responseID)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-15 (16)bit                         | RESPONSE BIT | UNUSED BIT   | DATALENGTH 18-31 (14)bit                  |
            // | 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 | 16           | 17           | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-65535                                    | VR: 0/1      | VR: 0/1      | VR: 0-16382                               | VR = VALUE RANGE
            // 
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  0           |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  0           |  1           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | UNUSEDBIT_MASK 0x4000
            // | 0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 |  1           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | RESPONSEBIT_MASK 0x8000
            // | 1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 |  0           |  0           |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

            fixed (byte* ptr = header)
            {
                uint h = *(uint*)ptr;
                commandID = (Constants.COMMANDID_MASK & h) >> 16;

                //uint unused2 = (Constants.UNUSED2BIT_MASK & h) >> 15;
                //uint unused1 = (Constants.UNUSED1BIT_MASK & h) >> 14;
                dataLenght = (int)(Constants.DATA_LENGTH_MASK & h);
                responseID = *(uint*)(ptr + 4);
            }
        }

        #endregion
    }
}