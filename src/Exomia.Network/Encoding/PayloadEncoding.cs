#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.CompilerServices;
using Exomia.Network.Lib;

namespace Exomia.Network.Encoding
{
    static unsafe class PayloadEncoding
    {
        private const ushort CONE  = 0b0000_0001_0000_0001;
        private const uint   C0    = 0x214EE939;
        private const uint   C1    = 0x117DFA89;
        private const byte   ONE   = 0b1000_0000;
        private const byte   MASK1 = 0b0111_1111;
        private const byte   MASK2 = 0b0100_0000;
        private const uint   H0    = 0x209536F9;

        private static readonly uint s_h0 = H0 ^ Math2.R1(H0, 12);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int EncodedPayloadLength(int length)
        {
            return length + Math2.Ceiling(length / 7.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int DecodedPayloadLength(int length)
        {
            return length - Math2.Ceiling(length / 8.0);
        }

        internal static ushort Encode(byte* data, int length, byte* buffer, out int bufferLength)
        {
            bufferLength = EncodedPayloadLength(length);
            uint checksum = s_h0;
            while (length > 7)
            {
                Encode(&checksum, buffer, data, 7);
                buffer += 8;
                data   += 7;
                length -= 7;
            }
            
            if (length > 0)
            {
                Encode(&checksum, buffer, data, length);
            }

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        internal static ushort Decode(byte* src, int length, byte* dst, out int dstLength)
        {
            dstLength = DecodedPayloadLength(length);
            uint checksum = s_h0;

            while (length > 8)
            {
                Decode(&checksum, dst, src, 8);
                dst    += 7;
                src    += 8;
                length -= 8;
            }
            
            if (length > 0)
            {
                Decode(&checksum, dst, src, length);
            }

            return (ushort)(CONE | ((ushort)checksum ^ (checksum >> 16)));
        }

        private static void Encode(uint* checksum, byte* buffer, byte* data, int size)
        {
            byte b = ONE;
            for (int i = 0; i < size; ++i)
            {
                uint d = *(data + i);
                byte s = (byte)(d >> 7);
                b             =  (byte)(b | (s << (6 - i)));
                *(buffer + i) =  (byte)(ONE | d);
                *checksum     ^= d + C0;
            }
            *(buffer + size) =  b;
            *checksum        += Math2.R1(b, 23) + C1;
        }

        private static void Decode(uint* checksum, byte* dest, byte* src, int size)
        {
            byte b = *((src + size) - 1);
            for (int i = 0; i < size - 1; ++i)
            {
                byte d = (byte)(((b & (MASK2 >> i)) << (i + 1)) | (*(src + i) & MASK1));
                *(dest + i) =  d;
                *checksum   ^= d + C0;
            }
            *checksum += Math2.R1(b, 23) + C1;
        }
    }
}