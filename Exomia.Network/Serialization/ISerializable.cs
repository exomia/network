#region License

// Copyright (c) 2018-2019, exomia
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
        ///     serialize the object to a byte array.
        /// </summary>
        /// <param name="length"> [out] the length of the data. </param>
        /// <returns>
        ///     serialized data.
        /// </returns>
        byte[] Serialize(out int length);

        /// <summary>
        ///     deserialize the object from a byte array.
        /// </summary>
        /// <param name="data"> . </param>
        void Deserialize(byte[] data);
    }
}