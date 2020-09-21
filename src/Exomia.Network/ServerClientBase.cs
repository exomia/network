#region License

// Copyright (c) 2018-2020, exomia
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
    ///     A server client base.
    /// </summary>
    /// <typeparam name="T"> Socket|EndPoint. </typeparam>
    public abstract class ServerClientBase<T> : IServerClient
        where T : class
    {
        private           DateTime _lastReceivedPacketTimeStamp;
        private protected T        _arg0;

        /// <inheritdoc />
        public abstract IPAddress IPAddress { get; }

        /// <inheritdoc />
        public DateTime LastReceivedPacketTimeStamp
        {
            get { return _lastReceivedPacketTimeStamp; }
        }

        internal T Arg0
        {
            get { return _arg0; }
            set { _arg0 = value; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerClientBase{T}" /> class.
        /// </summary>
        private protected ServerClientBase()
        {
            _arg0 = null!;
        }

        internal void SetLastReceivedPacketTimeStamp()
        {
            _lastReceivedPacketTimeStamp = DateTime.Now;
        }
    }
}