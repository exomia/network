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
    ///     Values that represent SendError.
    /// </summary>
    public enum SendError
    {
        /// <summary>
        ///     No error, all good
        /// </summary>
        None,

        /// <summary>
        ///     A socket exception is occured
        /// </summary>
        Socket,

        /// <summary>
        ///     The socket was disposed
        /// </summary>
        Disposed,

        /// <summary>
        ///     The SEND_FLAG is not set
        /// </summary>
        Invalid,

        /// <summary>
        ///     Unknown error occured
        /// </summary>
        Unknown
    }
}