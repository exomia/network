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
        /// <param name="arr"> byte array. </param>
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
        /// <param name="arr"> byte array. </param>
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