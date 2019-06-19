#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
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
using System.Net.Sockets;
using System.Threading;

namespace Exomia.Network
{
    /// <summary>
    ///     A socket asynchronous event arguments pool.
    /// </summary>
    class SocketAsyncEventArgsPool : IDisposable
    {
        /// <summary>
        ///     The buffer.
        /// </summary>
        private readonly SocketAsyncEventArgs[] _buffer;

        /// <summary>
        ///     The index.
        /// </summary>
        private int _index;

        /// <summary>
        ///     The lock.
        /// </summary>
        private SpinLock _lock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SocketAsyncEventArgsPool" /> class.
        /// </summary>
        /// <param name="numberOfBuffers"> (Optional) Number of buffers. </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public SocketAsyncEventArgsPool(uint numberOfBuffers = 32)
        {
            if (numberOfBuffers <= 0) { throw new ArgumentOutOfRangeException(nameof(numberOfBuffers)); }

            _lock   = new SpinLock(Debugger.IsAttached);
            _buffer = new SocketAsyncEventArgs[numberOfBuffers];
        }

        /// <summary>
        ///     Gets the rent.
        /// </summary>
        /// <returns>
        ///     The SocketAsyncEventArgs.
        /// </returns>
        public SocketAsyncEventArgs Rent()
        {
            SocketAsyncEventArgs buffer = null;

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

        /// <summary>
        ///     Returns the given arguments.
        /// </summary>
        /// <param name="args"> The Arguments to return. </param>
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