#region License

// Copyright (c) 2018-2019, exomia
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
    public abstract class ServerClientBase<T> where T : class
    {
        /// <summary>
        ///     Socket|Endpoint.
        /// </summary>
        private protected T _arg0;

        /// <summary>
        ///     The last received packet time stamp Date/Time.
        /// </summary>
        private DateTime _lastReceivedPacketTimeStamp;

        /// <summary>
        ///     Gets the Date/Time of the last received packet time stamp.
        /// </summary>
        /// <value>
        ///     The last received packet time stamp.
        /// </value>
        public DateTime LastReceivedPacketTimeStamp
        {
            get { return _lastReceivedPacketTimeStamp; }
        }

        /// <summary>
        ///     Gets the IP address.
        /// </summary>
        /// <value>
        ///     The IP address.
        /// </value>
        public abstract IPAddress IPAddress { get; }

        /// <summary>
        ///     Gets the argument 0.
        /// </summary>
        /// <value>
        ///     The argument 0.
        /// </value>
        internal T Arg0
        {
            get { return _arg0; }
            set { _arg0 = value; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerClientBase{T}" /> class.
        /// </summary>
        private protected ServerClientBase() { }

        /// <summary>
        ///     Sets last received packet time stamp.
        /// </summary>
        internal void SetLastReceivedPacketTimeStamp()
        {
            _lastReceivedPacketTimeStamp = DateTime.Now;
        }
    }
}