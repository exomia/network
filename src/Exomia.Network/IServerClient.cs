#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net;

namespace Exomia.Network
{
    /// <summary>
    ///     Interface for server client.
    /// </summary>
    public interface IServerClient
    {
        /// <summary>
        ///     Gets the Date/Time of the last received packet time stamp.
        /// </summary>
        /// <value>
        ///     The last received packet time stamp.
        /// </value>
        DateTime LastReceivedPacketTimeStamp { get; }

        /// <summary>
        ///     Gets the IP address.
        /// </summary>
        /// <value>
        ///     The IP address.
        /// </value>
        IPAddress IPAddress { get; }
    }
}