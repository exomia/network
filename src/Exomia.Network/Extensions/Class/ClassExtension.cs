#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.CompilerServices;
using Exomia.Network.Serialization;

namespace Exomia.Network.Extensions.Class
{
    /// <summary>
    ///     The class extensions.
    /// </summary>
    public static class ClassExtensions
    {
        /// <summary>
        ///     A byte[] extension method that initializes this object from the given from bytes.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="arr"> The arr to act on. </param>
        /// <param name="obj"> [out] The object. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj)
            where T : ISerializable, new()
        {
            obj = new T();
            obj.Deserialize(arr, 0, arr.Length);
        }

        /// <summary>
        ///     A byte[] extension method that initializes this object from the given from bytes.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="arr"> The arr to act on. </param>
        /// <returns>
        ///     A T.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr)
            where T : ISerializable, new()
        {
            T obj = new T();
            obj.Deserialize(arr, 0, arr.Length);
            return obj;
        }

        /// <summary>
        ///     A byte[] extension method that initializes this object from the given from bytes.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="arr">    The arr to act on. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="length"> The length. </param>
        /// <returns>
        ///     A T.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr, int offset, int length)
            where T : ISerializable, new()
        {
            T obj = new T();
            obj.Deserialize(arr, offset, length);
            return obj;
        }
    }
}