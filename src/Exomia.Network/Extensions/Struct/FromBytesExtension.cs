#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.CompilerServices;

namespace Exomia.Network.Extensions.Struct
{
    /// <summary>
    ///     from bytes extension.
    /// </summary>
    public static class FromBytesExtension
    {
        /// <summary>
        ///     Converts the given <paramref name="bytes" /> array into the given type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="bytes"> The bytes to act on. </param>
        /// <param name="value"> [out] The reinterpreted <paramref name="bytes"/> as <typeparamref name="T"/>. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytesUnsafe<T>(this byte[] bytes, out T value)
            where T : struct
        {
            value = Unsafe.As<byte, T>(ref bytes[0]);
        }

        /// <summary>
        ///     Converts the given <paramref name="bytes" /> array into the given type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="bytes">  The bytes to act on. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="value"> [out] The reinterpreted <paramref name="bytes"/> as <typeparamref name="T"/>. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytesUnsafe<T>(this byte[] bytes, int offset, out T value)
            where T : struct
        {
            value = Unsafe.As<byte, T>(ref bytes[offset]);
        }

        /// <summary>
        ///     Converts the given <paramref name="bytes" /> array into the given type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="bytes"> The bytes to act on. </param>
        /// <returns>
        ///     The reinterpreted <paramref name="bytes"/> as <typeparamref name="T"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytesUnsafe<T>(this byte[] bytes)
            where T : struct
        {
            return Unsafe.As<byte, T>(ref bytes[0]);
        }

        /// <summary>
        ///     Converts the given <paramref name="bytes" /> array into the given type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="bytes">  The bytes to act on. </param>
        /// <param name="offset"> The offset. </param>
        /// <returns>
        ///     The reinterpreted <paramref name="bytes"/> as <typeparamref name="T"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytesUnsafe<T>(this byte[] bytes, int offset)
            where T : struct
        {
            return Unsafe.As<byte, T>(ref bytes[offset]);
        }
    }
}