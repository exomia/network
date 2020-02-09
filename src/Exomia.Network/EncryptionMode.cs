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
    ///     Values that represent EncryptionMode.
    ///     MASK 0b00000111.
    /// </summary>
    public enum EncryptionMode : byte
    {
        /// <summary>
        ///     None
        /// </summary>
        None = 0b000,

        /// <summary>
        ///     End2End
        /// </summary>
        End2End = 0b100
    }
}