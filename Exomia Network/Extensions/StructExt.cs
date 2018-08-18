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
    ///     StructExt class
    /// </summary>
    public static class StructExt
    {
        #region ToBytes

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
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(data, ptr, true);

                byte[] arr = new byte[length];
                Marshal.Copy(ptr, arr, 0, length);
                return arr;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
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
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(data, ptr, true);
                arr = new byte[length];
                Marshal.Copy(ptr, arr, 0, length);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
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
        public static void ToBytes<T>(this T data, ref byte[] arr, int offset, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(data, ptr, true);
                Marshal.Copy(ptr, arr, offset, length);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
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
        public static byte[] ToBytes2<T>(this T data, out int length) where T : struct
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
        public static void ToBytes2<T>(this T data, out byte[] arr, out int length) where T : struct
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
        public static void ToBytes2<T>(this T data, ref byte[] arr, int offset, out int length) where T : struct
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

        #endregion

        #region FromBytes

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, 0, ptr, size);
                obj = Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, int offset, out T obj) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, offset, ptr, size);
                obj = Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr, int offset) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, offset, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe<T>(this byte[] arr, out T obj) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                obj = Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe<T>(this byte[] arr, int offset, out T obj) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                obj = Marshal.PtrToStructure<T>(new IntPtr(ptr + offset));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe<T>(this byte[] arr) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                return Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe<T>(this byte[] arr, int offset) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                return Marshal.PtrToStructure<T>(new IntPtr(ptr + offset));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe2<T>(this byte[] arr, out T obj) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                obj = *(T*)ptr;
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe2<T>(this byte[] arr, int offset, out T obj) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                obj = *(T*)(ptr + offset);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe2<T>(this byte[] arr) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                return *(T*)ptr;
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe2<T>(this byte[] arr, int offset) where T : unmanaged
        {
            fixed (byte* ptr = arr)
            {
                return *(T*)(ptr + offset);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes2<T>(this byte[] arr, out T obj) where T : struct
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
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset">offset</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes2<T>(this byte[] arr, int offset, out T obj) where T : struct
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
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes2<T>(this byte[] arr) where T : struct
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
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="offset"></param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes2<T>(this byte[] arr, int offset) where T : struct
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

        #endregion

        /*
         * NO GENERIC SOLUTION
         * 
         *  FromBytes(byte[] data)
         *  unsafe
         *  {
         *      fixed (byte* ptr = data)
         *      {
         *          STRUCT str = *(STRUCT*)ptr;
         *      }
         *  }
         *  
         *  
         *  ToBytes(STRUCT data)
         *  int len = Marshal.SizeOf(typeof(STRUCT));
         *  byte[] arr = new byte[len];
         *  unsafe
         *  {   
         *      //Marshal.Copy(new IntPtr(&data), arr, 0, len);
         *      fixed (byte* ptr = arr)
         *      {
         *          Memcpy(ptr, &ts, len);
         *      }
         *  }
         */
    }
}