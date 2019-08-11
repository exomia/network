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
    ///     Values that represent DisconnectReason.
    /// </summary>
    /// <remarks>
    ///     Can be used to determine if a client has disconnected properly.
    /// </remarks>
    public enum DisconnectReason
    {
        /// <summary>
        ///     Unspecified/Unknown Reason
        /// </summary>
        Unspecified,

        /// <summary>
        ///     Graceful
        /// </summary>
        Graceful,

        /// <summary>
        ///     Aborted
        /// </summary>
        Aborted,

        /// <summary>
        ///     Error
        /// </summary>
        Error
    }
}