#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Exomia.Network.Lib
{
    /// <summary>
    ///     custom event class for faster raise, add, remove operations.
    /// </summary>
    /// <typeparam name="T"> typeof delegate. </typeparam>
    sealed class Event<T> where T : Delegate
    {
        /// <summary>
        ///     The callbacks.
        /// </summary>
        private T[] _callbacks;

        /// <summary>
        ///     Number of.
        /// </summary>
        private int _count;

        /// <summary>
        ///     The lock.
        /// </summary>
        private SpinLock _lock;

        /// <summary>
        ///     the count of registered callbacks.
        /// </summary>
        /// <value>
        ///     The count.
        /// </value>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count; }
        }

        /// <summary>
        ///     the event list Attention: do not use Callbacks.Length use the <see cref="Count" />
        ///     Property instead.
        /// </summary>
        /// <param name="index"> Zero-based index of the entry to access. </param>
        /// <returns>
        ///     The indexed item.
        /// </returns>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _callbacks[index]; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Event{T}" /> class.
        /// </summary>
        /// <param name="capacity"> (Optional) initial capacity. </param>
        public Event(int capacity = 4)
        {
            _callbacks = new T[capacity];
            _lock      = new SpinLock(Debugger.IsAttached);
        }

        /// <summary>
        ///     adds a new callback to the event list.
        /// </summary>
        /// <param name="callback"> callback. </param>
        public void Add(T callback)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count >= _callbacks.Length)
                {
                    T[] buffer = new T[_callbacks.Length * 2];
                    Array.Copy(_callbacks, buffer, _count);
                    _callbacks = buffer;
                }
                _callbacks[_count++] = callback;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     removes a callback at the given index from the event list.
        /// </summary>
        /// <param name="index"> index. </param>
        public void Remove(int index)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                _callbacks[index]  = _callbacks[--_count];
                _callbacks[_count] = null!; //cleanup, ignore it cause the don't use it anyway!
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     removes a callback at the given index from the event list.
        /// </summary>
        /// <param name="item"> callback to remove. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
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
                        _callbacks[i]      = _callbacks[--_count];
                        _callbacks[_count] = null!; //cleanup, ignore it cause the don't use it anyway!
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