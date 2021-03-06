﻿#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Exomia.Network.Buffers
{
    static class ByteArrayPool
    {
        private static          SpinLock     s_lock;
        private static readonly byte[]?[]?[] s_buffers;
        private static readonly uint[]       s_index;
        private static readonly int[]        s_bufferLength;
        private static readonly int[]        s_bufferCount;

        /// <summary>
        ///     Initializes static members of the <see cref="ByteArrayPool" /> class.
        /// </summary>
        static ByteArrayPool()
        {
            s_lock = new SpinLock(Debugger.IsAttached);

            s_bufferLength = new[]
            {
                1 << 7, 1 << 8, 1 << 9, 1 << 10, 1 << 11, 1 << 12, 1 << 13, 1 << 14, 1 << 15, 1 << 16
            };
            s_bufferCount = new[] { 128, 128, 64, 64, 64, 32, 32, 16, 8, 8 };
            s_index       = new uint[s_bufferLength.Length];
            s_buffers     = new byte[s_bufferLength.Length][][];
        }

        internal static byte[] Rent(int size)
        {
            int bucketIndex = SelectBucketIndex(size);
            if (bucketIndex >= s_buffers.Length)
            {
                return new byte[size];
            }

            byte[]? buffer    = null;
            bool    lockTaken = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                s_buffers[bucketIndex] ??= new byte[s_bufferCount[bucketIndex]][];

                if (s_index[bucketIndex] < s_buffers[bucketIndex]!.Length)
                {
                    uint index = s_index[bucketIndex]++;
                    buffer                         = s_buffers[bucketIndex]![index];
                    s_buffers[bucketIndex]![index] = null;
                }
                return buffer ?? new byte[s_bufferLength[bucketIndex]];
            }
            finally
            {
                if (lockTaken) { s_lock.Exit(false); }
            }
        }

        internal static void Return(byte[] array)
        {
            int bucketIndex = SelectBucketIndex(array.Length);
            if (bucketIndex >= s_bufferLength.Length || array.Length != s_bufferLength[bucketIndex])
            {
                return;
            }

            bool lockTaken = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                if (s_index[bucketIndex] != 0)
                {
                    s_buffers[bucketIndex]![--s_index[bucketIndex]] = array;
                }
            }
            finally
            {
                if (lockTaken) { s_lock.Exit(false); }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SelectBucketIndex(int size)
        {
            uint br = ((uint)size - 1) >> 7;

            int index = 0;
            if (br > 0xFFFF)
            {
                br    >>= 16;
                index =   16;
            }
            if (br > 0xFF)
            {
                br    >>= 8;
                index +=  8;
            }
            if (br > 0xF)
            {
                br    >>= 4;
                index +=  4;
            }
            if (br > 0x3)
            {
                br    >>= 2;
                index +=  2;
            }
            if (br > 0x1)
            {
                br    >>= 1;
                index +=  1;
            }

            return index + (int)br;
        }
    }
}