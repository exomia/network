#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using Exomia.Network.Lib;

namespace Exomia.Network.Encoding
{
    /// <summary>
    ///     A payload encoding.
    /// </summary>
    static unsafe class PayloadEncoding
    {
        /// <summary>
        ///     The cone.
        /// </summary>
        private const ushort CONE = 0b0000_0001_0000_0001;

        /// <summary>
        ///     The c 0.
        /// </summary>
        private const uint C0 = 0x214EE939;

        /// <summary>
        ///     The first c.
        /// </summary>
        private const uint C1 = 0x117DFA89;

        /// <summary>
        ///     The one.
        /// </summary>
        private const byte ONE = 0b1000_0000;

        /// <summary>
        ///     The first mask.
        /// </summary>
        private const byte MASK1 = 0b0111_1111;

        /// <summary>
        ///     The second mask.
        /// </summary>
        private const byte MASK2 = 0b0100_0000;

        /// <summary>
        ///     The h 0.
        /// </summary>
        private const uint H0 = 0x209536F9;

        /// <summary>
        ///     The 0.
        /// </summary>
        private static readonly uint s_h0 = H0 ^ Math2.R1(H0, 12);

        /// <summary>
        ///     Decodes the data.
        /// </summary>
        /// <param name="src">          [in,out] If non-null, the decode source. </param>
        /// <param name="offset">       The offset. </param>
        /// <param name="length">       The length. </param>
        /// <param name="buffer">       The buffer. </param>
        /// <param name="bufferLength"> [out] Length of the buffer. </param>
        /// <returns>
        ///     An ushort.
        /// </returns>
        internal static ushort Decode(byte* src, int offset, int length, byte[] buffer, out int bufferLength)
        {
            bufferLength = length - Math2.Ceiling(length / 8.0);
            uint checksum = s_h0;
            int  o1       = 0;
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

        /// <summary>
        ///     Encodes the data.
        /// </summary>
        /// <param name="data">         The data. </param>
        /// <param name="offset">       The offset. </param>
        /// <param name="length">       The length. </param>
        /// <param name="buffer">       The buffer. </param>
        /// <param name="bufferOffset"> The buffer offset. </param>
        /// <param name="bufferLength"> [out] Length of the buffer. </param>
        /// <returns>
        ///     An ushort.
        /// </returns>
        internal static ushort Encode(byte[]  data, int offset, int length, byte[] buffer, int bufferOffset,
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

        /// <summary>
        ///     Decodes the data.
        /// </summary>
        /// <param name="checksum"> [in,out] If non-null, the checksum. </param>
        /// <param name="dest">     [in,out] If non-null, destination for the. </param>
        /// <param name="o1">       The first int. </param>
        /// <param name="src">      [in,out] If non-null, source for the. </param>
        /// <param name="o2">       The second int. </param>
        /// <param name="size">     The size. </param>
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

        /// <summary>
        ///     Encodes the data.
        /// </summary>
        /// <param name="checksum"> [in,out] If non-null, the checksum. </param>
        /// <param name="buffer">   The buffer. </param>
        /// <param name="o1">       The first int. </param>
        /// <param name="data">     The data. </param>
        /// <param name="o2">       The second int. </param>
        /// <param name="size">     The size. </param>
        private static void Encode(uint* checksum, byte[] buffer, int o1, byte[] data, int o2, int size)
        {
            byte b = ONE;
            for (int i = 0; i < size; ++i)
            {
                uint d = data[o2 + i];
                byte s = (byte)(d                 >> 7);
                b              =  (byte)(b   | (s << (6 - i)));
                buffer[o1 + i] =  (byte)(ONE | d);
                *checksum      ^= d + C0;
            }
            buffer[o1 + size] =  b;
            *checksum         += Math2.R1(b, 23) + C1;
        }
    }
}