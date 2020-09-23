#region License

// Copyright (c) 2018-2020, exomia
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
    abstract class BigDataHandler<TKey> : IDisposable where TKey : struct
    {
        private readonly Dictionary<TKey, Buffer> _bigDataBuffers;
        private          SpinLock                 _bigDataBufferLock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BigDataHandler{TKey}" /> class.
        /// </summary>
        protected BigDataHandler()
        {
            _bigDataBufferLock = new SpinLock(Debugger.IsAttached);
            _bigDataBuffers    = new Dictionary<TKey, Buffer>(16);
        }

        internal unsafe byte[]? Receive(TKey  key,
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
                        _bigDataBuffers.Add(key, bdb = Create(key, length));
                    }
                }
                finally
                {
                    if (lockTaken) { _bigDataBufferLock.Exit(false); }
                }
            }

            fixed (byte* dst = bdb._data)
            {
                Mem.Cpy(dst + chunkOffset, src, chunkLength);
            }

            if (bdb.AddBytes(chunkLength) == 0)
            {
                Remove(key);
                return bdb._data;
            }

            return null;
        }

        private protected abstract Buffer Create(TKey key, int length);

        private protected bool Remove(TKey key)
        {
            bool lockTaken = false;
            try
            {
                _bigDataBufferLock.Enter(ref lockTaken);
                return _bigDataBuffers.Remove(key);
            }
            finally
            {
                if (lockTaken) { _bigDataBufferLock.Exit(false); }
            }
        }

        internal class Default : BigDataHandler<TKey>
        {
            /// <inheritdoc />
            private protected override Buffer Create(TKey key, int length)
            {
                return new Buffer(ByteArrayPool.Rent(length), length);
            }
        }

        internal class Timed : BigDataHandler<TKey>
        {
            /// <inheritdoc />
            private protected override Buffer Create(TKey key, int length)
            {
                return new Buffer.Time(
                    ByteArrayPool.Rent(length), length, state =>
                    {
                        if (Remove(key))
                        {
                            ByteArrayPool.Return(((Buffer.Time)state)._data);
                        }
                        ((Buffer.Time)state).Dispose();
                    });
            }
        }

        private protected class Buffer : IDisposable
        {
            public readonly byte[]   _data;
            private         int      _bytesLeft;
            private         SpinLock _thisLock;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Buffer" /> struct.
            /// </summary>
            /// <param name="data">      The data. </param>
            /// <param name="bytesLeft"> The bytes left. </param>
            internal Buffer(byte[] data, int bytesLeft)
            {
                _data      = data;
                _bytesLeft = bytesLeft;
                _thisLock  = new SpinLock(Debugger.IsAttached);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal virtual int AddBytes(int count)
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

            internal class Time : Buffer
            {
                private const    int   TIMER_INTERVAL = 1500;
                private readonly Timer _timer;

                /// <summary>
                ///     Initializes a new instance of the <see cref="Buffer" /> struct.
                /// </summary>
                /// <param name="data">            The data. </param>
                /// <param name="bytesLeft">       The bytes left. </param>
                /// <param name="elapsedCallback"> The elapsed callback. </param>
                public Time(byte[] data, int bytesLeft, TimerCallback elapsedCallback)
                    : base(data, bytesLeft)
                {
                    _timer = new Timer(elapsedCallback, this, Timeout.Infinite, Timeout.Infinite);
                }

                /// <inheritdoc />
                protected override void OnDispose(bool disposing)
                {
                    if (disposing)
                    {
                        _timer.Dispose();
                    }
                }

                /// <inheritdoc />
                internal override int AddBytes(int count)
                {
                    int bytes = base.AddBytes(count);
                    if (bytes == 0) { _timer.Dispose(); }
                    if (bytes != 0) { _timer.Change(TIMER_INTERVAL, Timeout.Infinite); }
                    return bytes;
                }
            }

            #region IDisposable Support

            /// <summary>
            ///     true if the instance is already disposed; false otherwise.
            /// </summary>
            protected bool _disposed;

            /// <inheritdoc />
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged/managed resources.
            /// </summary>
            /// <param name="disposing"> true if user code; false called by finalizer. </param>
            private void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    OnDispose(disposing);
                    _disposed = true;
                }
            }

            /// <inheritdoc />
            ~Buffer()
            {
                Dispose(false);
            }

            /// <summary>
            ///     called then the instance is disposing.
            /// </summary>
            /// <param name="disposing"> true if user code; false called by finalizer. </param>
            protected virtual void OnDispose(bool disposing) { }

            #endregion
        }

        #region IDisposable Support

        /// <summary>
        ///     True if disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        ///     Releases the unmanaged resources used by the Exomia.Network.BigDataHandler&lt;TKey&gt; and optionally releases the
        ///     managed resources.
        /// </summary>
        /// <param name="disposing">  to release both managed and unmanaged resources;  to release only unmanaged resources. </param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    bool lockTaken = false;
                    try
                    {
                        _bigDataBufferLock.Enter(ref lockTaken);
                        foreach (KeyValuePair<TKey, Buffer> bigDataBuffer in _bigDataBuffers)
                        {
                            bigDataBuffer.Value.Dispose();
                        }
                        _bigDataBuffers.Clear();
                    }
                    finally
                    {
                        if (lockTaken) { _bigDataBufferLock.Exit(false); }
                    }
                }
                _disposed = true;
            }
        }

        /// <inheritdoc />
        ~BigDataHandler()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}