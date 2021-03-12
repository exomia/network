#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Runtime.CompilerServices;

namespace Exomia.Network
{
    /// <summary> Handler, called when the deserialize packet. </summary>
    /// <typeparam name="TResult"> Type of the result. </typeparam>
    /// <param name="packet"> The packet. </param>
    /// <param name="result"> [out] The result. </param>
    /// <returns> <c>true</c> if deserialization was successful; <c>false</c> otherwise. </returns>
    public delegate bool DeserializePacketHandler<TResult>(in Packet packet, out TResult result);

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

        /// <summary>
        ///     Convert this packets raw data into a utf8 string representation.
        /// </summary>
        /// <returns>
        ///     A utf8 string that represents this packets raw data.
        /// </returns>
        public override string ToString()
        {
            return ToString(System.Text.Encoding.UTF8);
        }

        /// <summary>
        ///     Convert this packets raw data into a string representation.
        /// </summary>
        /// <param name="encoding"> The encoding. </param>
        /// <returns>
        ///     A string that represents this packets raw data.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(System.Text.Encoding encoding)
        {
            return (encoding ?? throw new ArgumentNullException(nameof(encoding))).GetString(Buffer, Offset, Length);
        }
    }
}