﻿using System;
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
        private static readonly int[] s_index;
        private static readonly int[] s_bufferLength;

        #endregion

        #region Constructors

        static ByteArrayPool()
        {
            s_lock = new SpinLock(Debugger.IsAttached);

            s_bufferLength = new[] { 128, 256, 512, 1024, 4096, 8192, 16384 };
            s_index = new int[s_bufferLength.Length];
            s_buffers = new byte[s_bufferLength.Length][][];
        }

        #endregion

        #region Methods

        internal static byte[] Rent(int size)
        {
            int index = SelectBucketIndex(size);

            byte[] buffer = null;
            bool lockTaken = false, allocateBuffer = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                if (s_buffers[index] == null)
                {
                    s_buffers[index] = new byte[(s_bufferLength.Length - index) * BUFFER_LENGTH_FACTOR][];
                }

                if (s_index[index] < s_buffers[index].Length)
                {
                    buffer = s_buffers[index][s_index[index]];
                    s_buffers[s_index[index]++] = null;
                    allocateBuffer = buffer == null;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    s_lock.Exit(false);
                }
            }

            return !allocateBuffer ? buffer : new byte[s_bufferLength[index]];
        }

        internal static void Return(byte[] array)
        {
            int index = SelectBucketIndex(array.Length);
            if (array.Length != s_bufferLength[index])
            {
                throw new ArgumentException(nameof(array));
            }

            bool lockTaken = false;
            try
            {
                s_lock.Enter(ref lockTaken);

                if (s_index[index] != 0)
                {
                    s_buffers[index][--s_index[index]] = array;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    s_lock.Exit(false);
                }
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