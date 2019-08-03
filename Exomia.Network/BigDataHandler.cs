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
        internal unsafe byte[] Receive(int   key,
                                       byte* src,
                                       int   chunkLength,
                                       int   chunkOffset,
                                       int   length)
        {
            if (!_bigDataBuffers.TryGetValue(key, out Buffer bdb))
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
                finally
                {
                    if (lockTaken) { _bigDataBufferLock.Exit(false); }
                }
            }

            lock (bdb)
            {
                fixed (byte* dst2 = bdb.Data)
                {
                    Mem.Cpy(dst2 + chunkOffset, src, chunkLength);
                }
                bdb.BytesLeft -= chunkLength;
                if (bdb.BytesLeft == 0)
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
            public int BytesLeft;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Buffer" /> struct.
            /// </summary>
            /// <param name="data">      The data. </param>
            /// <param name="bytesLeft"> The bytes left. </param>
            public Buffer(byte[] data, int bytesLeft)
            {
                Data      = data;
                BytesLeft = bytesLeft;
            }
        }
    }
}