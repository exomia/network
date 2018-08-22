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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exomia.Network.Extensions.Struct
{
    /// <summary>
    ///     ToBytesExtensions class
    /// </summary>
    public static class ToBytesExtensions
    {
        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="length">out the size of T</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToBytesUnsafe<T>(this T data, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            byte[] arr = new byte[length];
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr), true);
            }
            return arr;
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe<T>(this T data, out byte[] arr, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            arr = new byte[length];
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr), true);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe<T>(this T data, ref byte[] arr, int offset, out int length)
            where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr + offset), true);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="length">out the size of T</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToBytesUnsafe2<T>(this T data, out int length) where T : unmanaged
        {
            length = sizeof(T);
            byte[] arr = new byte[length];
            fixed (byte* ptr = arr)
            {
                *(T*)ptr = data;
            }
            return arr;
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe2<T>(this T data, out byte[] arr, out int length) where T : unmanaged
        {
            length = sizeof(T);
            arr = new byte[length];
            fixed (byte* ptr = arr)
            {
                *(T*)ptr = data;
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe2<T>(this T data, ref byte[] arr, int offset, out int length)
            where T : unmanaged
        {
            length = Marshal.SizeOf(typeof(T));
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr + offset), true);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="length">out the size of T</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes<T>(this T data, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            byte[] arr = new byte[length];
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
                return arr;
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes<T>(this T data, out byte[] arr, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            arr = new byte[length];
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">ref byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">out the size of T</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes<T>(this T data, ref byte[] arr, int offset, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject() + offset, false);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}