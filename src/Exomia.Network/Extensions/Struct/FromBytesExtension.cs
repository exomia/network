#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exomia.Network.Extensions.Struct
{
    /// <summary>
    ///     from bytes extensions.
    /// </summary>
    public static class FromBytesExtension
    {
        /// <summary>
        ///     converts a byte array into a struct.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="arr"> The byte array. </param>
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
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
        /// <param name="arr"> The byte array. </param>
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
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
        /// <param name="arr"> The byte array. </param>
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
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
        /// <param name="arr"> The byte array. </param>
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
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
        /// <param name="arr"> The byte array. </param>
        /// <param name="obj"> [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="obj">    [out] out struct. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, int offset, out T obj) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject() + offset, typeof(T))!;
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
        /// <param name="arr"> The byte array. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
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
        /// <param name="arr">    The byte array. </param>
        /// <param name="offset"> The offset. </param>
        /// <returns>
        ///     struct.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr, int offset) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject() + offset, typeof(T))!;
            }
            finally
            {
                handle.Free();
            }
        }
    }
}