#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System;
using System.Net;

namespace Exomia.Network
{
    /// <summary>
    ///     ServerClientBase{T} class
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    public abstract class ServerClientBase<T> where T : class
    {
        /// <summary>
        ///     Socket|Endpoint
        /// </summary>
        protected T _arg0;

        private DateTime _lastReceivedPacketTimeStamp;

        /// <summary>
        ///     LastReceivedPacketTimeStamp
        /// </summary>
        public DateTime LastReceivedPacketTimeStamp
        {
            get { return _lastReceivedPacketTimeStamp; }
        }

        /// <summary>
        ///     IPAddress
        /// </summary>
        public abstract IPAddress IPAddress { get; }

        /// <summary>
        ///     EndPoint
        /// </summary>
        public abstract EndPoint EndPoint { get; }

        /// <summary>
        ///     ServerClientBase constructor
        /// </summary>
        /// <param name="arg0">Socket|Endpoint</param>
        protected ServerClientBase(T arg0)
        {
            _arg0 = arg0;
        }

        internal void SetLastReceivedPacketTimeStamp()
        {
            _lastReceivedPacketTimeStamp = DateTime.Now;
        }
    }
}