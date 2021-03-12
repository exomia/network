#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace Exomia.Network
{
    class SocketAsyncEventArgsPool : IDisposable
    {
        private readonly SocketAsyncEventArgs?[] _buffer;
        private          int                     _index;
        private          SpinLock                _lock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SocketAsyncEventArgsPool" /> class.
        /// </summary>
        /// <param name="numberOfBuffers"> (Optional) Number of buffers. </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public SocketAsyncEventArgsPool(ushort numberOfBuffers = 32)
        {
            if (numberOfBuffers == 0) { throw new ArgumentOutOfRangeException(nameof(numberOfBuffers)); }

            _lock   = new SpinLock(Debugger.IsAttached);
            _buffer = new SocketAsyncEventArgs[numberOfBuffers];
        }

        public SocketAsyncEventArgs? Rent()
        {
            SocketAsyncEventArgs? buffer = null;

            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index < _buffer.Length)
                {
                    buffer            = _buffer[_index];
                    _buffer[_index++] = null;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _lock.Exit(false);
                }
            }

            return buffer;
        }

        public void Return(SocketAsyncEventArgs args)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index != 0)
                {
                    _buffer[--_index] = args;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _lock.Exit(false);
                }
            }
        }

        #region IDisposable Support

        /// <summary>
        ///     True to disposed value.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        ///     Releases the unmanaged resources used by the Exomia.Network.SocketAsyncEventArgsPool and
        ///     optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     True to release both managed and unmanaged resources; false to
        ///     release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    for (int i = 0; i < _index; ++i)
                    {
                        _buffer[i]?.Dispose();
                    }
                }

                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}