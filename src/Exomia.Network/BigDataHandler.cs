#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Native;

namespace Exomia.Network
{
    /// <summary>
    ///     A big data handler.
    /// </summary>
    class BigDataHandler
    {
        /// <summary>
        ///     The big data buffers.
        /// </summary>
        private readonly Dictionary<int, Buffer> _bigDataBuffers;

        /// <summary>
        ///     The big data buffer lock.
        /// </summary>
        private SpinLock _bigDataBufferLock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BigDataHandler" /> class.
        /// </summary>
        public BigDataHandler()
        {
            _bigDataBufferLock = new SpinLock(Debugger.IsAttached);
            _bigDataBuffers    = new Dictionary<int, Buffer>(16);
        }

        /// <summary>
        ///     Receives.
        /// </summary>
        /// <param name="key">         The key. </param>
        /// <param name="src">         [in,out] If non-null, source for the. </param>
        /// <param name="chunkLength"> Length of the chunk. </param>
        /// <param name="chunkOffset"> The chunk offset. </param>
        /// <param name="length">      The length. </param>
        /// <returns>
        ///     A byte[] or null.
        /// </returns>
        internal unsafe byte[]? Receive(int   key,
                                        byte* src,
                                        int   chunkLength,
                                        int   chunkOffset,
                                        int   length)
        {
            if (!_bigDataBuffers!.TryGetValue(key, out Buffer? bdb))
            {
                bool lockTaken = false;
                try
                {
                    _bigDataBufferLock.Enter(ref lockTaken);
                    if (!_bigDataBuffers.TryGetValue(key, out bdb))
                    {
                        _bigDataBuffers.Add(
                            key,
                            bdb = new Buffer(ByteArrayPool.Rent(length), length));
                    }
                }
                catch { throw new NullReferenceException(nameof(bdb)); }
                finally
                {
                    if (lockTaken) { _bigDataBufferLock.Exit(false); }
                }
            }

            fixed (byte* dst2 = bdb.Data)
            {
                Mem.Cpy(dst2 + chunkOffset, src, chunkLength);
            }

            if (bdb.AddBytes(chunkLength) == 0)
            {
                bool lockTaken = false;
                try
                {
                    _bigDataBufferLock.Enter(ref lockTaken);
                    _bigDataBuffers.Remove(key);
                }
                finally
                {
                    if (lockTaken) { _bigDataBufferLock.Exit(false); }
                }
                return bdb.Data;
            }

            return null;
        }

        /// <summary>
        ///     Buffer for big data.
        /// </summary>
        private class Buffer
        {
            /// <summary>
            ///     The data.
            /// </summary>
            public readonly byte[] Data;

            /// <summary>
            ///     The bytes left.
            /// </summary>
            private int _bytesLeft;

            /// <summary>
            ///     The big data buffer lock.
            /// </summary>
            private SpinLock _thisLock;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Buffer" /> struct.
            /// </summary>
            /// <param name="data">      The data. </param>
            /// <param name="bytesLeft"> The bytes left. </param>
            public Buffer(byte[] data, int bytesLeft)
            {
                Data       = data;
                _bytesLeft = bytesLeft;
                _thisLock  = new SpinLock(Debugger.IsAttached);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddBytes(int count)
            {
                bool lockTaken = false;
                try
                {
                    _thisLock.Enter(ref lockTaken);
                    return _bytesLeft -= count;
                }
                finally
                {
                    if (lockTaken) { _thisLock.Exit(false); }
                }
            }
        }
    }
}