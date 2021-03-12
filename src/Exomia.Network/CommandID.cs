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
    ///     CommandID.
    /// </summary>
    static class CommandID
    {
        /// <summary>
        ///     CONNECT_ID.
        /// </summary>
        public const ushort CONNECT = 65534;

        /// <summary>
        ///     DISCONNECT_ID.
        /// </summary>
        public const ushort DISCONNECT = 65533;

        /// <summary>
        ///     IDENTIFICATION_ID.
        /// </summary>
        public const ushort IDENTIFICATION = 65532;

        /// <summary>
        ///     PING_ID.
        /// </summary>
        public const ushort PING = 65531;
    }
}