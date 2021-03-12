#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

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
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="length"> [out] The size of T. </param>
        /// <returns>
        ///     The byte array representing the <paramref name="data" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytesUnsafe<T>(this T data, out int length)
            where T : struct
        {
            byte[] bytes = new byte[length = Marshal.SizeOf(typeof(T))];
            Unsafe.As<byte, T>(ref bytes[0]) = data;
            return bytes;
        }

        /// <summary>
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="bytes">  [out] The bytes. </param>
        /// <param name="length"> [out] The size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytesUnsafe<T>(this T data, out byte[] bytes, out int length)
            where T : struct
        {
            bytes                            = new byte[length = Marshal.SizeOf(typeof(T))];
            Unsafe.As<byte, T>(ref bytes[0]) = data;
        }

        /// <summary>
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="bytes">  [in,out] The bytes. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="length"> [out] The size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytesUnsafe<T>(this T data, ref byte[] bytes, int offset, out int length)
            where T : struct
        {
            length                                = Marshal.SizeOf(typeof(T));
            Unsafe.As<byte, T>(ref bytes[offset]) = data;
        }

        /// <summary>
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="length"> [out] The size of T. </param>
        /// <returns>
        ///     The byte array representing the <paramref name="data" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToBytesUnsafe2<T>(this T data, out int length)
            where T : unmanaged
        {
            byte[] bytes = new byte[length = sizeof(T)];
            Unsafe.As<byte, T>(ref bytes[0]) = data;
            return bytes;
        }

        /// <summary>
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="bytes">  [out] The bytes. </param>
        /// <param name="length"> [out] The size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe2<T>(this T data, out byte[] bytes, out int length)
            where T : unmanaged
        {
            bytes                            = new byte[length = sizeof(T)];
            Unsafe.As<byte, T>(ref bytes[0]) = data;
        }

        /// <summary>
        ///     Converts the given <paramref name="data" /> struct into a byte array.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="data">   The data. </param>
        /// <param name="bytes">  [in,out] The bytes. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="length"> [out] The size of T. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe2<T>(this T data, ref byte[] bytes, int offset, out int length)
            where T : unmanaged
        {
            length                                = sizeof(T);
            Unsafe.As<byte, T>(ref bytes[offset]) = data;
        }
    }
}