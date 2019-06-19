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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exomia.Network.Extensions.Struct
{
    /// <summary>
    ///     from bytes extensions.
    /// </summary>
    public static class FromBytesExtensions
    {
        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <param name="obj"> [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe<T>(this byte[] arr, out T obj) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                obj = Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="obj">    [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe<T>(this byte[] arr, int offset, out T obj) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                obj = Marshal.PtrToStructure<T>(new IntPtr(ptr + offset));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe<T>(this byte[] arr) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                return Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe<T>(this byte[] arr, int offset) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                return Marshal.PtrToStructure<T>(new IntPtr(ptr + offset));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <param name="obj"> [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe2<T>(this byte[] arr, out T obj) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                obj = *(T*)ptr;
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="obj">    [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe2<T>(this byte[] arr, int offset, out T obj) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                obj = *(T*)(ptr + offset);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe2<T>(this byte[] arr) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                return *(T*)ptr;
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe2<T>(this byte[] arr, int offset) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                return *(T*)(ptr + offset);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <param name="obj"> [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="obj">    [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, int offset, out T obj) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject() + offset, typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> byte array. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr">    byte array. </param>
        /// <param name="offset"> . </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr, int offset) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject() + offset, typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }
    }
}