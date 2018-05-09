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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Exomia.Network.Buffers
{
    internal static class ByteArrayPool
    {
        #region Variables

        private const int BUFFER_LENGTH_FACTOR = 20;

        private static SpinLock s_lock;
        private static readonly byte[][][] s_buffers;
        private static readonly uint[] s_index;
        private static readonly int[] s_bufferLength;

        #endregion

        #region Constructors

        static ByteArrayPool()
        {
            s_lock = new SpinLock(Debugger.IsAttached);

            s_bufferLength = new[] { 128, 256, 512, 1024, 4096, 8192, 16384 };
            s_index = new uint[s_bufferLength.Length];
            s_buffers = new byte[s_bufferLength.Length][][];
        }

        #endregion

        #region Methods

        internal static byte[] Rent(int size)
        {
            int bucketIndex = SelectBucketIndex(size);

            byte[] buffer = null;
            bool lockTaken = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                if (s_buffers[bucketIndex] == null)
                {
                    s_buffers[bucketIndex] = new byte[(s_bufferLength.Length - bucketIndex) * BUFFER_LENGTH_FACTOR][];
                }

                if (s_index[bucketIndex] < s_buffers[bucketIndex].Length)
                {
                    uint index = s_index[bucketIndex]++;
                    buffer = s_buffers[bucketIndex][index];
                    s_buffers[bucketIndex][index] = null;
                }
            }
            finally
            {
                if (lockTaken) { s_lock.Exit(false); }
            }

            return buffer ?? new byte[s_bufferLength[bucketIndex]];
        }

        internal static void Return(byte[] array)
        {
            int bucketIndex = SelectBucketIndex(array.Length);
            if (array.Length != s_bufferLength[bucketIndex])
            {
                throw new ArgumentException(nameof(array));
            }

            bool lockTaken = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                if (s_index[bucketIndex] != 0)
                {
                    s_buffers[bucketIndex][--s_index[bucketIndex]] = array;
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
                br >>= 16;
                index = 16;
            }
            if (br > 0xFF)
            {
                br >>= 8;
                index += 8;
            }
            if (br > 0xF)
            {
                br >>= 4;
                index += 4;
            }
            if (br > 0x3)
            {
                br >>= 2;
                index += 2;
            }
            if (br > 0x1)
            {
                br >>= 1;
                index += 1;
            }

            return index + (int)br;
        }

        #endregion
    }
}