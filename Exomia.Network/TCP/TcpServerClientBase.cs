﻿#region License

// Copyright (c) 2018-2019, exomia
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
            get { return (_arg0.RemoteEndPoint as IPEndPoint)?.Address; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TcpServerClientBase" /> class.
        /// </summary>
        /// <param name="socket"> The socket. </param>
        protected TcpServerClientBase(Socket socket)
            : base(socket) { }
    }
}