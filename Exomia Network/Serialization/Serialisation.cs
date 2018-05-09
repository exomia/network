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
        internal static byte[] Serialize(uint commandID, uint type, byte[] data, int lenght, uint responseID)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-9 (10)bit | TYPE 10-17 (8)bit       | DATALENGTH 18-31 (14)bit                  |
            // | 0 1 2 3 4 5 6 7 8 9   | 10 11 12 13 14 15 16 17 | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-1023            | VR: 0-255               | VR: 0-16383                               | VR = VALUE RANGE
            // 
            // | 0 0 0 0 0 0 0 0 0 0   |  0  0  0  0  0  0  0  0 |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0 0 0 0 0 0 0 0 0 0   |  1  1  1  1  1  1  1  1 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | TYPE_MASK 0x3FC000
            // | 1 1 1 1 1 1 1 1 1 1   |  0  0  0  0  0  0  0  0 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFC00000

            byte[] buffer = ByteArrayPool.Rent(Constants.HEADER_SIZE + lenght);

            uint header = (commandID << 22) | (type << 14) | (ushort)lenght;
            fixed (byte* ptr = buffer)
            {
                uint* uptr = (uint*)ptr;
                *(uptr + 0) = header;
                *(uptr + 1) = responseID;
            }

            //DATA
            Buffer.BlockCopy(data, 0, buffer, Constants.HEADER_SIZE, lenght);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out uint type, out int dataLenght,
            out uint responseID)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-9 (10)bit | TYPE 10-17 (8)bit       | DATALENGTH 18-31 (14)bit                  |
            // | 0 1 2 3 4 5 6 7 8 9   | 10 11 12 13 14 15 16 17 | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-1023            | VR: 0-255               | VR: 0-16383                               | VR = VALUE RANGE
            // 
            // | 0 0 0 0 0 0 0 0 0 0   |  0  0  0  0  0  0  0  0 |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0 0 0 0 0 0 0 0 0 0   |  1  1  1  1  1  1  1  1 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | TYPE_MASK 0x3FC000
            // | 1 1 1 1 1 1 1 1 1 1   |  0  0  0  0  0  0  0  0 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFC00000

            fixed (byte* ptr = header)
            {
                uint* uptr = (uint*)ptr;
                uint h = *(uptr + 0);

                commandID = (Constants.COMMANDID_MASK & h) >> 22;
                type = (Constants.TYPE_MASK & h) >> 14;
                dataLenght = (int)(Constants.DATA_LENGTH_MASK & h);

                responseID = *(uptr + 1);
            }
        }

        #endregion
    }
}