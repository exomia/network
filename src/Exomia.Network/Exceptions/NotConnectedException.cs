#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Runtime.Serialization;

namespace Exomia.Network.Exceptions
{
    /// <summary>
    ///     Exception for signalling not connected errors.
    /// </summary>
    public class NotConnectedException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NotConnectedException" /> class.
        /// </summary>
        public NotConnectedException() { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NotConnectedException" /> class.
        /// </summary>
        /// <param name="message"> The message. </param>
        public NotConnectedException(string message)
            : base(message) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NotConnectedException" /> class.
        /// </summary>
        /// <param name="message">        The message. </param>
        /// <param name="innerException"> The inner exception. </param>
        public NotConnectedException(string message, Exception innerException)
            : base(message, innerException) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NotConnectedException" /> class.
        /// </summary>
        /// <param name="info">        The serialization info. </param>
        /// <param name="context"> The streaming context. </param>
        protected NotConnectedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}