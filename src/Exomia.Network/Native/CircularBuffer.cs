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
using System.Runtime.InteropServices;
using System.Threading;

namespace Exomia.Network.Native
{
    /// <summary>
    ///     A circular buffer class.
    /// </summary>
    unsafe class CircularBuffer : IDisposable
    {
        /// <summary>
        ///     The pointer.
        /// </summary>
        private readonly IntPtr _mPtr;

        /// <summary>
        ///     The pointer.
        /// </summary>
        private readonly byte* _ptr;

        /// <summary>
        ///     The capacity.
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        ///     The mask.
        /// </summary>
        private readonly int _mask;

        /// <summary>
        ///     The head.
        /// </summary>
        private int _head;

        /// <summary>
        ///     The tail.
        /// </summary>
        private int _tail;

        /// <summary>
        ///     Number of.
        /// </summary>
        private int _count;

        /// <summary>
        ///     The lock.
        /// </summary>
        private SpinLock _lock;

        /// <summary>
        ///     Maximum capacity of the buffer. Elements pushed into the buffer after maximum capacity is
        ///     reached will remove an element.
        /// </summary>
        /// <value>
        ///     The capacity.
        /// </value>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _capacity; }
        }

        /// <summary>
        ///     <c>true</c> if the circular buffer is empty; <c>false</c> otherwise.
        /// </summary>
        /// <value>
        ///     True if this object is empty, false if not.
        /// </value>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count == 0; }
        }

        /// <summary>
        ///     current used bytes.
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
        ///     Initializes a new instance of the <see cref="CircularBuffer" /> class.
        /// </summary>
        /// <param name="capacity"> (Optional) capacity (pow2) </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when one or more arguments are outside
        ///     the required range.
        /// </exception>
        public CircularBuffer(int capacity = 1024)
        {
            _lock = new SpinLock(Debugger.IsAttached);

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            uint value = (uint)capacity;
            if (value > 0x80000000)
            {
                throw new ArgumentOutOfRangeException();
            }
            value--;
            value     |= value >> 1;
            value     |= value >> 2;
            value     |= value >> 4;
            value     |= value >> 8;
            value     |= value >> 16;
            _capacity =  (int)(value + 1);

            if (_capacity <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            _mask = _capacity - 1;

            _mPtr = Marshal.AllocHGlobal(_capacity);
            _ptr  = (byte*)_mPtr;

            Clear();
        }

        /// <summary>
        ///     clear the circular buffer.
        /// </summary>
        public void Clear()
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                Mem.Set(_ptr, 0, _capacity);
                _head  = 0;
                _tail  = 0;
                _count = 0;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     read a piece from the buffer.
        /// </summary>
        /// <param name="dst">    the destination array. </param>
        /// <param name="offset"> the offset. </param>
        /// <param name="length"> length. </param>
        /// <param name="skip">   skip bytes. </param>
        /// <returns>
        ///     An int.
        /// </returns>
        public int Read(byte[] dst, int offset, int length, int skip)
        {
            fixed (byte* ptr = dst)
            {
                return Read(ptr + offset, length, skip);
            }
        }

        /// <summary>
        ///     read a piece from the buffer.
        /// </summary>
        /// <param name="dst">    [in,out] the destination array. </param>
        /// <param name="length"> length. </param>
        /// <param name="skip">   skip bytes. </param>
        /// <returns>
        ///     An int.
        /// </returns>
        public int Read(byte* dst, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                if (_tail + skip + length < _capacity)
                {
                    Mem.Cpy(dst, _ptr + _tail + skip, length);
                }
                else if (_tail + skip < _capacity)
                {
                    int l1 = _capacity - (_tail + skip);
                    Mem.Cpy(dst, _ptr + _tail + skip, l1);
                    Mem.Cpy(dst + l1, _ptr, length - l1);
                }
                else
                {
                    Mem.Cpy(dst, _ptr + ((_tail + skip) & _mask), length);
                }

                _tail  =  (_tail + skip + length) & _mask;
                _count -= skip + length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     peek a piece from the buffer.
        /// </summary>
        /// <param name="dst">    the destination array. </param>
        /// <param name="offset"> the offset. </param>
        /// <param name="length"> the length you want to read from the buffer. </param>
        /// <param name="skip">   the bytes to skip. </param>
        /// <returns>
        ///     a byte array.
        /// </returns>
        public int Peek(byte[] dst, int offset, int length, int skip)
        {
            fixed (byte* ptr = dst)
            {
                return Peek(ptr + offset, length, skip);
            }
        }

        /// <summary>
        ///     peek a piece from the buffer.
        /// </summary>
        /// <param name="dst">    [in,out] destination array. </param>
        /// <param name="length"> the length you want to read from the buffer. </param>
        /// <param name="skip">   the bytes to skip. </param>
        /// <returns>
        ///     a byte array.
        /// </returns>
        public int Peek(byte* dst, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                if (_tail + skip + length < _capacity)
                {
                    Mem.Cpy(dst, _ptr + _tail + skip, length);
                }
                else if (_tail + skip < _capacity)
                {
                    int l1 = _capacity - (_tail + skip);
                    Mem.Cpy(dst, _ptr + _tail + skip, l1);
                    Mem.Cpy(dst + l1, _ptr, length - l1);
                }
                else
                {
                    Mem.Cpy(dst, _ptr + ((_tail + skip) & _mask), length);
                }

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     Peek byte.
        /// </summary>
        /// <param name="offset"> the offset. </param>
        /// <param name="b">      [out] The out byte to process. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekByte(int offset, out byte b)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0 || _count <= offset)
                {
                    b = 0;
                    return false;
                }
                b = *(_ptr + ((_tail + offset) & _mask));
                return true;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     Peek header.
        /// </summary>
        /// <param name="skip">         skip bytes. </param>
        /// <param name="packetHeader"> [out] The packet header. </param>
        /// <param name="commandID">    [out] Identifier for the command. </param>
        /// <param name="dataLength">   [out] Length of the data. </param>
        /// <param name="checksum">     [out] The checksum. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        public bool PeekHeader(int        skip,
                               out byte   packetHeader,
                               out uint   commandID,
                               out int    dataLength,
                               out ushort checksum)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0 || _count < skip + Constants.TCP_HEADER_SIZE)
                {
                    commandID    = 0;
                    dataLength   = 0;
                    packetHeader = 0;
                    checksum     = 0;
                    return false;
                }

                if (_tail + skip + Constants.TCP_HEADER_SIZE < _capacity)
                {
                    packetHeader = *(_ptr + _tail + skip);
                    uint h2 = *(uint*)(_ptr + _tail + skip + 1);
                    commandID  = h2 >> Constants.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Constants.DATA_LENGTH_MASK);
                    checksum   = *(ushort*)(_ptr + _tail + skip + 5);
                }
                else if (_tail + skip < _capacity)
                {
                    packetHeader = *(_ptr + ((_tail + skip) & _mask));
                    uint h2 = (uint)((*(_ptr + ((_tail + skip + 4) & _mask)) << 24)
                                   | (*(_ptr + ((_tail + skip + 3) & _mask)) << 16)
                                   | (*(_ptr + ((_tail + skip + 2) & _mask)) << 8)
                                   | *(_ptr + ((_tail + skip + 1) & _mask)));
                    commandID  = h2 >> Constants.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Constants.DATA_LENGTH_MASK);
                    checksum = (ushort)(
                        (*(_ptr + ((_tail + skip + 6) & _mask)) << 8)
                      | *(_ptr + ((_tail + skip + 5) & _mask)));
                }
                else
                {
                    packetHeader = *(_ptr + ((_tail + skip) & _mask));
                    uint h2 = *(uint*)(_ptr + ((_tail + skip + 1) & _mask));
                    commandID  = h2 >> Constants.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Constants.DATA_LENGTH_MASK);
                    checksum   = *(ushort*)(_ptr + ((_tail + skip + 5) & _mask));
                }

                return true;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     skips until a specified byte is found.
        /// </summary>
        /// <param name="offset"> the offset. </param>
        /// <param name="value">  the value to compare with. </param>
        /// <returns>
        ///     <c>true</c> if the value was found; <c>false otherwise</c>
        /// </returns>
        public bool SkipUntil(int offset, byte value)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count > offset)
                {
                    int i = offset;
                    while (i < _count)
                    {
                        if (*(_ptr + ((_tail + i++) & _mask)) == value)
                        {
                            _tail  =  (_tail + i) & _mask;
                            _count -= i;
                            return true;
                        }
                    }
                    _tail  = (_tail + _count) & _mask;
                    _count = 0;
                }
                return false;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     skips a specified count.
        /// </summary>
        /// <param name="count"> the count to skip. </param>
        public void Skip(int count)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (count > _count)
                {
                    count = _count;
                }
                _tail  =  (_tail + count) & _mask;
                _count -= count;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     write a piece of data into the buffer
        ///     attention:
        ///     if you write to much unread data will be overridden.
        /// </summary>
        /// <param name="value">  the value to compare with. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="length"> the length. </param>
        /// <returns>
        ///     the bytes written to the buffer.
        /// </returns>
        public int Write(byte[] value, int offset, int length)
        {
            fixed (byte* src = value)
            {
                return Write(src + offset, length);
            }
        }

        /// <summary>
        ///     write a piece of data into the buffer
        ///     attention:
        ///     if you write to much unread data will be overridden.
        /// </summary>
        /// <param name="src">    [in,out] the source array. </param>
        /// <param name="length"> the length. </param>
        /// <returns>
        ///     the bytes written to the buffer.
        /// </returns>
        public int Write(byte* src, int length)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_count + length > _capacity)
                {
                    length = _capacity - _count;
                }

                if (_head + length < _capacity)
                {
                    Mem.Cpy(_ptr + _head, src, length);
                }
                else
                {
                    int l1 = _capacity - _head;
                    Mem.Cpy(_ptr + _head, src, l1);
                    Mem.Cpy(_ptr, src + l1, length - l1);
                }

                _head  =  (_head + length) & _mask;
                _count += length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        #region IDisposable Support

        /// <summary>
        ///     True to disposed value.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        ///     Releases the unmanaged resources used by the Exomia.Network.Native.CircularBuffer and
        ///     optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     True to release both managed and unmanaged resources; false to
        ///     release only unmanaged resources.
        /// </param>
        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Marshal.FreeHGlobal(_mPtr);
                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        ~CircularBuffer()
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