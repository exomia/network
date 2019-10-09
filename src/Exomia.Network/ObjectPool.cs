#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics;
using System.Threading;

namespace Exomia.Network
{
    /// <summary>
    ///     An object pool. This class cannot be inherited.
    /// </summary>
    /// <typeparam name="T"> Generic type parameter. </typeparam>
    sealed class ObjectPool<T> where T : class
    {
        /// <summary>
        ///     The buffers.
        /// </summary>
        private readonly T?[] _buffers;

        /// <summary>
        ///     The index.
        /// </summary>
        private int _index;

        /// <summary>
        ///     The lock.
        /// </summary>
        private SpinLock _lock;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ObjectPool{T}" /> class.
        /// </summary>
        /// <param name="numberOfBuffers"> Number of buffers. </param>
        public ObjectPool(ushort numberOfBuffers = 32)
        {
            _lock    = new SpinLock(Debugger.IsAttached);
            _buffers = new T[numberOfBuffers];
        }

        /// <summary>
        ///     Gets the rent.
        /// </summary>
        /// <returns>
        ///     A ServerClientStateObject.
        /// </returns>
        internal T? Rent()
        {
            T?   buffer    = null;
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index < _buffers.Length)
                {
                    buffer             = _buffers[_index];
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

        /// <summary>
        ///     Returns the given object.
        /// </summary>
        /// <param name="obj"> The Object to return. </param>
        internal void Return(T obj)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index != 0)
                {
                    _buffers[--_index] = obj;
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
    }
}