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

namespace Exomia.Network.Lib
{
    /// <summary>
    ///     custom event class for faster raise, add, remove operations
    /// </summary>
    /// <typeparam name="T">typeof delegate</typeparam>
    sealed class Event<T> where T : Delegate
    {
        //TODO: calculate and use BlockCopy
        private readonly int _sizeOf;
        private T[] _callbacks;
        private int _count;

        private SpinLock _lock;

        /// <summary>
        ///     the count of registered callbacks
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count; }
        }

        /// <summary>
        ///     the event list
        ///     Attention: do not use Callbacks.Length use the <see cref="Count" /> Property instead
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _callbacks[index]; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Event{T}" /> class.
        /// </summary>
        /// <param name="capacity">initial capacity</param>
        public Event(int capacity = 4)
        {
            _callbacks = new T[capacity];
            _lock = new SpinLock(Debugger.IsAttached);

            //_sizeOf = Marshal.SizeOf();
        }

        /// <summary>
        ///     adds a new callback to the event list
        /// </summary>
        /// <param name="callback">callback</param>
        public void Add(T callback)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count >= _callbacks.Length)
                {
                    T[] buffer = new T[_callbacks.Length * 2];

                    //Buffer.BlockCopy(_callbacks, 0, buffer, 0, _count * _sizeOf);
                    Array.Copy(_callbacks, buffer, _count);
                    Interlocked.Exchange(ref _callbacks, buffer);
                }
                _callbacks[_count++] = callback;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     removes a callback at the given index from the event list
        /// </summary>
        /// <param name="index">index</param>
        public void Remove(int index)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                _callbacks[index] = _callbacks[--_count];
                _callbacks[_count] = null;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     removes a callback at the given index from the event list
        /// </summary>
        /// <param name="item">callback to remove</param>
        public bool Remove(T item)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                for (int i = 0; i < _count; i++)
                {
                    if (item == _callbacks[i])
                    {
                        _callbacks[i] = _callbacks[--_count];
                        _callbacks[_count] = null;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }
    }
}