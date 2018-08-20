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
using System.Runtime.InteropServices;
using System.Threading;

namespace Exomia.Network.Native
{
    /// <summary>
    ///     ByteArrayAllocator class
    /// </summary>
    public unsafe class ByteArrayAllocator : IDisposable
    {
        private readonly IntPtr _mPtr;
        private readonly byte* _ptr;
        private int _head;

        private SpinLock _lock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ByteArrayAllocator" /> class.
        /// </summary>
        /// <param name="capacity">capacity (pow2)</param>
        public ByteArrayAllocator(int capacity = 262144) //0,25mb
        {
            uint value = (uint)capacity;
            if (value > 0x80000000)
            {
                throw new ArgumentOutOfRangeException();
            }
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            capacity = (int)(value + 1);

            _mPtr = Marshal.AllocHGlobal(capacity);
            _ptr = (byte*)_mPtr;

            *(int*)_ptr = _head = 8;
            *(int*)(_ptr + 4) = capacity - 8;

            _lock = new SpinLock(Debugger.IsAttached);
        }

        /// <summary>
        ///     Allocate a new byte array
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public byte* Allocate(int length)
        {
            int next = *(int*)(_ptr + _head - 8);
            int free = *(int*)(_ptr + _head - 4);

            if (_head + length + 8 < free)
            {
                byte* ptr = _ptr + _head;
                *(int*)ptr = next;
                *(int*)(ptr + 4) = free - length - 8;

                _head += length + 8;
                return ptr + 8;
            }
            throw new AccessViolationException();
        }

        /// <summary>
        ///     free a byte array
        /// </summary>
        /// <param name="ptr"></param>
        public void Free(byte* ptr)
        {
            *(int*)(ptr - 8) = _head;
        }

        #region IDisposable Support

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Marshal.FreeHGlobal(_mPtr);
                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        ~ByteArrayAllocator()
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