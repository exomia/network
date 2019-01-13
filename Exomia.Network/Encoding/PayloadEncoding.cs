#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
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

using Exomia.Network.Lib;

namespace Exomia.Network.Encoding
{
    static unsafe class PayloadEncoding
    {
        private const ushort CONE = 0b0000_0001_0000_0001;

        private const uint C0 = 0x214EE939;
        private const uint C1 = 0x117DFA89;

        private const byte ONE = 0b1000_0000;
        private const byte MASK1 = 0b0111_1111;
        private const byte MASK2 = 0b0100_0000;

        private const uint H0 = 0x209536F9;
        private static readonly uint s_h0 = H0 ^ Math2.R1(H0, 12);

        internal static ushort Decode(byte* src, int offset, int length, byte[] buffer, out int bufferLength)
        {
            bufferLength = length - Math2.Ceiling(length / 8.0);
            uint checksum = s_h0;
            int o1 = 0;
            fixed (byte* dest = buffer)
            {
                while (offset + 8 < length)
                {
                    Decode(&checksum, dest, o1, src, offset, 8);
                    o1     += 7;
                    offset += 8;
                }
                Decode(&checksum, dest, o1, src, offset, length - offset);
            }

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        internal static ushort Encode(byte[] data, int offset, int length, byte[] buffer, int bufferOffset,
            out int bufferLength)
        {
            bufferLength = length + Math2.Ceiling(length / 7.0f);
            uint checksum = s_h0;
            while (offset + 7 < length)
            {
                Encode(&checksum, buffer, bufferOffset, data, offset, 7);
                bufferOffset += 8;
                offset       += 7;
            }
            Encode(&checksum, buffer, bufferOffset, data, offset, length - offset);

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Decode(uint* checksum, byte* dest, int o1, byte* src, int o2, int size)
        {
            byte b = *((src + o2 + size) - 1);
            for (int i = 0; i < size - 1; ++i)
            {
                byte d = (byte)(((b & (MASK2 >> i)) << (i + 1)) | (*(src + o2 + i) & MASK1));
                *(dest + o1 + i) =  d;
                *checksum        ^= d + C0;
            }
            *checksum += Math2.R1(b, 23) + C1;
        }

        private static void Encode(uint* checksum, byte[] buffer, int o1, byte[] data, int o2, int size)
        {
            byte b = ONE;
            for (int i = 0; i < size; ++i)
            {
                uint d = data[o2 + i];
                byte s = (byte)(d >> 7);
                b              =  (byte)(b | (s << (6 - i)));
                buffer[o1 + i] =  (byte)(ONE | d);
                *checksum      ^= d + C0;
            }
            buffer[o1 + size] =  b;
            *checksum         += Math2.R1(b, 23) + C1;
        }
    }
}