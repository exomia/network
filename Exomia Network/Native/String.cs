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
using System.Runtime.InteropServices;

namespace Exomia.Network.Native
{
    /// <inheritdoc />
    /// <summary>
    ///     native String class
    /// </summary>
    public unsafe class String : IDisposable
    {
        private readonly IntPtr _mPtr;
        private readonly char* _ptr;
        private readonly int _length;

        /// <summary>
        ///     return the length of the current string
        /// </summary>
        public int Length
        {
            get { return _length; }
        }

        /// <inheritdoc />
        public String(string value)
            : this(value, 0, value.Length) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Exomia.Network.Native.String" /> class.
        /// </summary>
        /// <param name="value">managed string value</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length</param>
        public String(string value, int offset, int length)
            : this(length)
        {
            fixed (char* src = value)
            {
                Mem.Cpy(_ptr, src + offset, _length * sizeof(char));
            }
        }

        private String(int length)
        {
            _length = length;
            _mPtr = Marshal.AllocHGlobal(length * sizeof(char));
            _ptr = (char*)_mPtr;
        }

        /// <summary>
        ///     concat two strings together
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static String operator +(String a, String b)
        {
            String s = new String(a.Length + b.Length);
            Mem.Cpy(s._ptr, a._ptr, a.Length * sizeof(char));
            Mem.Cpy(s._ptr + a.Length, b._ptr, b.Length * sizeof(char));
            return s;
        }

        /// <summary>
        ///     Convert to a managed string type
        /// </summary>
        /// <param name="value">value</param>
        /// <returns>a managed string</returns>
        public static explicit operator string(String value)
        {
            return new string(value._ptr, 0, value._length);
        }

        /// <summary>
        ///     Convert to a unmanaged string type
        /// </summary>
        /// <param name="value">value</param>
        /// <returns>a unmanaged string</returns>
        public static explicit operator String(string value)
        {
            return new String(value);
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
        ~String()
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