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
    ///     Handler, called when the deserialize packet.
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
        ///     The buffer.
        /// </summary>
        public readonly byte[] Buffer;

        /// <summary>
        ///     The offset.
        /// </summary>
        public readonly int Offset;

        /// <summary>
        ///     The length.
        /// </summary>
        public readonly int Length;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Packet" /> struct.
        /// </summary>
        /// <param name="buffer"> The buffer. </param>
        /// <param name="offset"> The offset. </param>
        /// <param name="length"> The length. </param>
        public Packet(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }
    }
}