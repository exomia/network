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
    ///     A serialization.
    /// </summary>
    static partial class Serialization
    {
        /// <summary>
        ///     The response bit mask.
        /// </summary>
        internal const uint RESPONSE_BIT_MASK = 0b01000000;

        /// <summary>
        ///     The response bit.
        /// </summary>
        private const byte RESPONSE_1_BIT = 1 << 6;

        /// <summary>
        ///     The compressed mode mask.
        /// </summary>
        internal const byte COMPRESSED_MODE_MASK = 0b00111000;

        /// <summary>
        ///     The command identifier shift.
        /// </summary>
        internal const int COMMAND_ID_SHIFT = 16;

        /// <summary>
        ///     The data length mask.
        /// </summary>
        internal const int DATA_LENGTH_MASK = 0xFFFF;

        /// <summary>
        ///     LENGTH_THRESHOLD 4096.
        /// </summary>
        private const int LENGTH_THRESHOLD = 1 << 12;
    }
}