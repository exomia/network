#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     DeserializePacket callback.
    /// </summary>
    /// <typeparam name="TResult"> Type of the result. </typeparam>
    /// <param name="packet"> The packet. </param>
    /// <returns>
    ///     A TResult.
    /// </returns>
    public delegate TResult DeserializePacketHandler<out TResult>(in Packet packet);

    /// <summary>
    ///     Packet readonly struct.
    /// </summary>
    public readonly struct Packet
    {
        /// <summary>
        ///     Buffer.
        /// </summary>
        public readonly byte[] Buffer;

        /// <summary>
        ///     Offset.
        /// </summary>
        public readonly int Offset;

        /// <summary>
        ///     Length.
        /// </summary>
        public readonly int Length;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Packet" /> struct.
        /// </summary>
        /// <param name="buffer"> . </param>
        /// <param name="offset"> . </param>
        /// <param name="length"> . </param>
        public Packet(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }
    }
}