#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network.Serialization
{
    /// <summary>
    ///     Interface for serializable.
    /// </summary>
    public interface ISerializable
    {
        /// <summary>
        ///     Serialize the object to a byte array.
        /// </summary>
        /// <param name="length"> [out] the length of the data. </param>
        /// <returns>
        ///     serialized data.
        /// </returns>
        byte[] Serialize(out int length);

        /// <summary>
        ///     Deserialize the object from a byte array.
        /// </summary>
        /// <param name="data">   The data. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="length"> The length of the data. </param>
        void Deserialize(byte[] data, int offset, int length);
    }
}