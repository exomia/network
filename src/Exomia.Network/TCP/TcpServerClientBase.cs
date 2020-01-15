#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Net;
using System.Net.Sockets;

namespace Exomia.Network.TCP
{
    /// <summary>
    ///     A TCP server client base.
    /// </summary>
    public abstract class TcpServerClientBase : ServerClientBase<Socket>
    {
        /// <inheritdoc />
        public override IPAddress IPAddress
        {
            get { return ((IPEndPoint)_arg0.RemoteEndPoint).Address; }
        }
    }
}