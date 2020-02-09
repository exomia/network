#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     Information about the packet.
    /// </summary>
    unsafe struct PacketInfo
    {
        /// <summary>
        ///     Identifier for the packet.
        /// </summary>
        public int PacketID;

        /// <summary>
        ///     Identifier for the command.
        /// </summary>
        public uint CommandID;

        /// <summary>
        ///     Identifier for the response.
        /// </summary>
        public uint ResponseID;

        /// <summary>
        ///     Source for the.
        /// </summary>
        public byte* Src;

        /// <summary>
        ///     Length of the chunk.
        /// </summary>
        public int ChunkLength;

        /// <summary>
        ///     The chunk offset.
        /// </summary>
        public int ChunkOffset;

        /// <summary>
        ///     The length.
        /// </summary>
        public int Length;

        /// <summary>
        ///     True if this object is chunked.
        /// </summary>
        public bool IsChunked;

        /// <summary>
        ///     The compression mode.
        /// </summary>
        public CompressionMode CompressionMode;

        /// <summary>
        ///     Length of the compressed.
        /// </summary>
        public int CompressedLength;
    }
}