#region License

// Copyright (c) 2018-2020, exomia
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
    ///     to bytes extensions.
    /// </summary>
    public static class ToBytesExtension
    {
        /// <summary>
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="length"> [out] out the size of T. </param>
        /// <returns>
        ///     byte array.
        /// </returns>
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [out] out byte array. </param>
        /// <param name="length"> [out] out the size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe<T>(this T data, out byte[] arr, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            arr    = new byte[length];
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr), true);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [in,out] out byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="length"> [out] out the size of T. </param>
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="length"> [out] out the size of T. </param>
        /// <returns>
        ///     byte array.
        /// </returns>
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [out] out byte array. </param>
        /// <param name="length"> [out] out the size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe2<T>(this T data, out byte[] arr, out int length) where T : unmanaged
        {
            length = sizeof(T);
            arr    = new byte[length];
            fixed (byte* ptr = arr)
            {
                *(T*)ptr = data;
            }
        }

        /// <summary>
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [in,out] out byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="length"> [out] out the size of T. </param>
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="length"> [out] out the size of T. </param>
        /// <returns>
        ///     byte array.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes<T>(this T data, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            byte[]   arr    = new byte[length];
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [out] out byte array. </param>
        /// <param name="length"> [out] out the size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes<T>(this T data, out byte[] arr, out int length) where T : struct
        {
            length = Marshal.SizeOf(typeof(T));
            arr    = new byte[length];
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
        ///     converts a struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="arr">    [in,out] ref byte array. </param>
        /// <param name="offset"> offset. </param>
        /// <param name="length"> [out] out the size of T. </param>
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