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
using Exomia.Network.Lib;
using LZ4;

namespace Exomia.Network.Serialization
{
    //TODO: UNIT TEST
    static unsafe partial class Serialization
    {
        private const ushort CONE = 0b1000_0000_0000_0001;

        private const uint C0 = 0x214EE939;
        private const uint C1 = 0x117DFA89;

        private const byte ONE = 0b1000_0000;
        private const byte MASK1 = 0b0111_1111;
        private const byte MASK2 = 0b0100_0000;

        private const uint H0 = 0x209536F9;
        private static readonly uint s_h0 = H0 ^ Math2.R1(H0, 12);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SerializeTcp(uint commandID, byte[] data, int offset, int length, uint responseID,
            EncryptionMode encryptionMode, out byte[] send, out int size)
        {
            send = ByteArrayPool.Rent(Constants.TCP_HEADER_SIZE + 9 + length + Math2.Ceiling(length / 7.0f));
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

            // 16bit   -    CHECKSUM

            int l;
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
                        checksum = Serialize(buffer, 0, s, send, Constants.TCP_HEADER_SIZE + 8, out l);
                        size     = Constants.TCP_HEADER_SIZE + 9 + l;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(RESPONSE_1_BIT | COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(l + 9) & DATA_LENGTH_MASK)
                                | (commandID << COMMANDID_SHIFT);
                            *(ushort*)(ptr + 5)                              = checksum;
                            *(uint*)(ptr + 7)                                = responseID;
                            *(int*)(ptr + 11)                                = length;
                            *(int*)(ptr + Constants.TCP_HEADER_SIZE + l + 8) = Constants.ZERO_BYTE;
                        }
                        return;
                    }
                }

                checksum = Serialize(data, offset, length, send, Constants.TCP_HEADER_SIZE + 4, out l);
                size     = Constants.TCP_HEADER_SIZE + 5 + l;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)(RESPONSE_1_BIT | (byte)encryptionMode);
                    *(uint*)(ptr + 1) =
                        ((uint)(l + 5) & DATA_LENGTH_MASK)
                        | (commandID << COMMANDID_SHIFT);
                    *(ushort*)(ptr + 5)                              = checksum;
                    *(uint*)(ptr + 7)                                = responseID;
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
                        checksum = Serialize(buffer, 0, s, send, Constants.TCP_HEADER_SIZE + 4, out l);
                        size     = Constants.TCP_HEADER_SIZE + 5 + l;
                        fixed (byte* ptr = send)
                        {
                            *ptr = (byte)(COMPRESSED_1_BIT | (byte)encryptionMode);
                            *(uint*)(ptr + 1) =
                                ((uint)(l + 5) & DATA_LENGTH_MASK)
                                | (commandID << COMMANDID_SHIFT);
                            *(ushort*)(ptr + 5)                              = checksum;
                            *(int*)(ptr + 7)                                 = length;
                            *(int*)(ptr + Constants.TCP_HEADER_SIZE + l + 4) = Constants.ZERO_BYTE;
                        }
                        return;
                    }
                }

                checksum = Serialize(data, offset, length, send, Constants.TCP_HEADER_SIZE, out l);
                size     = Constants.TCP_HEADER_SIZE + 1 + l;
                fixed (byte* ptr = send)
                {
                    *ptr = (byte)encryptionMode;
                    *(uint*)(ptr + 1) =
                        ((uint)(l + 1) & DATA_LENGTH_MASK)
                        | (commandID << COMMANDID_SHIFT);
                    *(ushort*)(ptr + 5)                          = checksum;
                    *(int*)(ptr + Constants.TCP_HEADER_SIZE + l) = Constants.ZERO_BYTE;
                }
            }
        }

        internal static ushort Deserialize(byte* src, int offset, int length, byte[] buffer, out int bufferLength)
        {
            bufferLength = length - Math2.Ceiling(length / 8.0);
            uint checksum = s_h0;
            int o1 = 0;
            fixed (byte* dest = buffer)
            {
                while (offset + 8 < length)
                {
                    Deserialize(&checksum, dest, o1, src, offset, 8);
                    o1     += 7;
                    offset += 8;
                }
                Deserialize(&checksum, dest, o1, src, offset, length - offset);
            }

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Deserialize(uint* checksum, byte* dest, int o1, byte* src, int o2, int size)
        {
            byte b = *(src + o2 + size - 1);
            for (int i = 0; i < size - 1; ++i)
            {
                byte d = (byte)(((b & (MASK2 >> i)) << (i + 1)) | (*(src + o2 + i) & MASK1));
                *(dest + o1 + i) =  d;
                *checksum        ^= d + C0;
            }
            *checksum += Math2.R1(b, 23) + C1;
        }

        internal static ushort Serialize(byte[] data, int offset, int length, byte[] buffer, int bufferOffset,
            out int bufferLength)
        {
            bufferLength = length + Math2.Ceiling(length / 7.0f);
            uint checksum = s_h0;
            while (offset + 7 < length)
            {
                Serialize(&checksum, buffer, bufferOffset, data, offset, 7);
                bufferOffset += 8;
                offset       += 7;
            }
            Serialize(&checksum, buffer, bufferOffset, data, offset, length - offset);

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Serialize(uint* checksum, byte[] buffer, int o1, byte[] data, int o2, int size)
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