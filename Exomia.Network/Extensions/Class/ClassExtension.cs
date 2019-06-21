#region License

// Copyright (c) 2018-2019, exomia
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
        ///     returns a new deserialized object from a byte array.
        /// </summary>
        /// <typeparam name="T"> ISerializable. </typeparam>
        /// <param name="arr"> The byte array. </param>
        /// <param name="obj"> [out] out object. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj)
            where T : ISerializable, new()
        {
            obj = new T();
            obj.Deserialize(arr);
        }

        /// <summary>
        ///     returns a new deserialized object from a byte array.
        /// </summary>
        /// <typeparam name="T"> ISerializable. </typeparam>
        /// <param name="arr"> The byte array. </param>
        /// <returns>
        ///     returns a new deserialized object from a byte array.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr)
            where T : ISerializable, new()
        {
            T obj = new T();
            obj.Deserialize(arr);
            return obj;
        }
    }
}