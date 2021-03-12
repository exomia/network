#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net;

namespace Exomia.Network.UDP
{
    /// <summary>
    ///     An UDP server client base.
    /// </summary>
    public abstract class UdpServerClientBase : ServerClientBase<EndPoint>
    {
        /// <inheritdoc />
        public override IPAddress IPAddress
        {
            get { return ((IPEndPoint)_arg0).Address; }
        }
    }
}