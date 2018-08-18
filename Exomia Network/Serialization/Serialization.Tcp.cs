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
        private const ushort CONE = 0b0000_0000_0000_0001;
        private const long L_OFFSET_MAX = int.MaxValue + 1L;

        private const uint C0 = 0x214EE939;
        private const uint C1 = 0x117DFA89;

        private const byte ONE = 0b1000_0000;
        private const byte MASK1 = 0b0111_1111;
        private const byte MASK2 = 0b0100_0000;

        private const uint H0 = 0x209536F9;
        private static readonly uint s_h0 = H0 ^ R1(H0, 12);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint commandID, byte[] data, int offset, int length, uint responseID,
            EncryptionMode encryptionMode, out byte[] send, out int size)
        {
            send = ByteArrayPool.Rent(Constants.TCP_HEADER_SIZE + 8 + length);
            SerializeTcp(commandID, data, offset, length, responseID, encryptionMode, send, out size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint commandID, byte[] data, int offset, int length, uint responseID,
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
                        data, offset, length, send, Constants.TCP_HEADER_SIZE + 8, length);

                    if (s > Constants.TCP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }

                    if (s > 0)
                    {
                        size = Constants.TCP_HEADER_SIZE + 8 + s;
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

                size = Constants.TCP_HEADER_SIZE + 4 + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                    *(uint*)(ptr + 1) =
                        ((uint)(length + 4) & DATA_LENGTH_MASK) |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                    *(uint*)(ptr + 5) = responseID;
                }
                Buffer.BlockCopy(data, offset, send, Constants.TCP_HEADER_SIZE + 4, length);
            }
            else
            {
                if (length >= LENGTH_THRESHOLD)
                {
                    int s = LZ4Codec.Encode(
                        data, offset, length, send, Constants.TCP_HEADER_SIZE + 4, length);
                    if (s > Constants.TCP_PACKET_SIZE_MAX)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"packet size of {Constants.TCP_PACKET_SIZE_MAX} exceeded (s: {s})");
                    }
                    if (s > 0)
                    {
                        size = Constants.TCP_HEADER_SIZE + 4 + s;
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

                size = Constants.TCP_HEADER_SIZE + length;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)encryptionMode;
                    *(uint*)(ptr + 1) =
                        ((uint)length & DATA_LENGTH_MASK) |
                        ((commandID << COMMANDID_SHIFT) & COMMANDID_MASK);
                }
                Buffer.BlockCopy(data, offset, send, Constants.TCP_HEADER_SIZE, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint R1(uint a, int b)
        {
            return (a << b) | (a >> (32 - b));
        }

        /// <summary>
        ///     Returns the smallest integer greater than or equal to the specified floating-point number.
        /// </summary>
        /// <param name="f">A floating-point number with single precision</param>
        /// <returns>The smallest integer, which is greater than or equal to f.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Ceiling(double f)
        {
            return (int)(L_OFFSET_MAX - (long)(L_OFFSET_MAX - f));
        }

        private static ushort Serialize(byte[] data, out byte[] buffer)
        {
            buffer = new byte[data.Length + Ceiling(data.Length / 7.0f)];
            uint checksum = s_h0;
            int o1 = 0;
            int o2 = 0;
            int dl = data.Length;
            while (o2 + 7 < dl)
            {
                Serialize(&checksum, buffer, o1, data, o2, 7);
                o1 += 8;
                o2 += 7;
            }
            Serialize(&checksum, buffer, o1, data, o2, dl - o2);

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Serialize(uint* checksum, byte[] buffer, int o1, byte[] data, int o2, int size)
        {
            byte b = ONE;
            for (int i = 0; i < size; i++)
            {
                uint d = data[o2 + i];
                byte s = (byte)(d >> 7);
                b = (byte)(b | (s << (6 - i)));
                buffer[o1 + i] = (byte)(ONE | d);
                *checksum ^= d + C0;
            }
            buffer[o1 + size] = b;
            *checksum += R1(b, 23) + C1;
        }

        private static ushort Deserialize(byte[] data, out byte[] buffer)
        {
            buffer = new byte[data.Length - Ceiling(data.Length / 8.0f)];

            uint checksum = s_h0;
            int o1 = 0;
            int o2 = 0;
            int dl = data.Length;
            while (o2 + 8 < dl)
            {
                Deserialize(&checksum, buffer, o1, data, o2, 8);
                o1 += 7;
                o2 += 8;
            }
            Deserialize(&checksum, buffer, o1, data, o2, dl - o2);

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Deserialize(uint* checksum, byte[] buffer, int o1, byte[] data, int o2, int size)
        {
            byte b = data[o2 + size - 1];
            for (int i = 0; i < size - 1; i++)
            {
                byte d = (byte)(((b & (MASK2 >> i)) << (i + 1)) | (data[o2 + i] & MASK1));
                buffer[o1 + i] = d;
                *checksum ^= d + C0;
            }
            *checksum += R1(b, 23) + C1;
        }
    }
}