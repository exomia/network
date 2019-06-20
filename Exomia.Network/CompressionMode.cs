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
    ///     Values that represent CompressionMode.
    ///     MASK 0b00111000.
    /// </summary>
    public enum CompressionMode : byte
    {
        /// <summary>
        ///     None
        /// </summary>
        None = 0b000000,

        /// <summary>
        ///     LZ4
        /// </summary>
        Lz4 = 0b100000
    }
}