#region License

// Copyright (c) 2018-2021, exomia
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
        public int             PacketID;
        public ushort          CommandOrResponseID;
        public ushort          RequestID;
        public bool            IsResponse;
        public byte*           Src;
        public int             ChunkLength;
        public int             ChunkOffset;
        public int             Length;
        public bool            IsChunked;
        public CompressionMode CompressionMode;
        public int             CompressedLength;
    }
}