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
using System.Net.Sockets;
using System.Threading;

namespace Exomia.Network
{
    internal class SocketAsyncEventArgsPool
    {
        #region Variables

        private readonly SocketAsyncEventArgs[] _buffers;
        private int _index;

        private SpinLock _lock;

        #endregion

        #region Constructors

        public SocketAsyncEventArgsPool(int numberOfBuffers = 32)
        {
            if (numberOfBuffers <= 0) { throw new ArgumentOutOfRangeException(nameof(numberOfBuffers)); }

            _lock = new SpinLock(Debugger.IsAttached);
            _buffers = new SocketAsyncEventArgs[numberOfBuffers];
        }

        #endregion

        #region Methods

        public SocketAsyncEventArgs Rent()
        {
            SocketAsyncEventArgs buffer = null;

            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index < _buffers.Length)
                {
                    buffer = _buffers[_index];
                    _buffers[_index++] = null;
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
                    _buffers[--_index] = args;
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

        #endregion
    }
}