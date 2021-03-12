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
    ///     A server client base.
    /// </summary>
    /// <typeparam name="T"> Socket|EndPoint. </typeparam>
    public abstract class ServerClientBase<T> : IServerClient
        where T : class
    {
        private readonly  Guid     _guid;
        private           DateTime _lastReceivedPacketTimeStamp;
        private protected T        _arg0;

        /// <inheritdoc />
        public abstract IPAddress IPAddress { get; }

        /// <inheritdoc />
        public DateTime LastReceivedPacketTimeStamp
        {
            get { return _lastReceivedPacketTimeStamp; }
        }

        /// <summary>
        ///     Gets a unique identifier.
        /// </summary>
        /// <value>
        ///     The identifier of the unique.
        /// </value>
        public Guid Guid
        {
            get { return _guid; }
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
            : this(Guid.NewGuid()) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerClientBase{T}" /> class.
        /// </summary>
        /// <param name="guid"> The identifier of the unique. </param>
        private protected ServerClientBase(Guid guid)
        {
            _guid = guid;
            _arg0 = null!;
        }

        internal void SetLastReceivedPacketTimeStamp()
        {
            _lastReceivedPacketTimeStamp = DateTime.Now;
        }
    }
}