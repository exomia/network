#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.CompilerServices;
using Exomia.Network.Extensions.Class;
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Serialization;

namespace Exomia.Network.Extensions.Packet
{
    /// <summary>
    ///     A packet extension.
    /// </summary>
    public static class PacketExtension
    {
        /// <summary>
        ///     A Network.Packet extension method that converts a packet to a structure.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <returns>
        ///     Packet as a T.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToStruct<T>(this Network.Packet packet) where T : struct
        {
            return packet.Buffer.FromBytesUnsafe<T>(packet.Offset);
        }

        /// <summary>
        ///     A Network.Packet extension method that deserialize this packet to the class.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="packet"> The packet. </param>
        /// <returns>
        ///     The given data deserialized to a T.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ToClass<T>(this Network.Packet packet) where T : ISerializable, new()
        {
            return packet.Buffer.FromBytes<T>(packet.Offset, packet.Length);
        }
    }
}