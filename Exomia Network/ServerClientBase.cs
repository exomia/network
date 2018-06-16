#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
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
        #region Variables

        /// <summary>
        ///     called than the client info is changed
        /// </summary>
        public event ClientInfoHandler<ServerClientBase<T>, T> ClientInfoChanged;

        /// <summary>
        ///     Socket|Endpoint
        /// </summary>
        protected T _arg0;

        private object _clientInfo;

        private DateTime _lastReceivedPacketTimeStamp = DateTime.Now;

        #endregion

        #region Properties

        /// <summary>
        ///     ClientInfo
        /// </summary>
        public object ClientInfo
        {
            get { return _clientInfo; }
        }

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

        #endregion

        #region Constructors

        /// <summary>
        ///     ServerClientBase constructor
        /// </summary>
        /// <param name="arg0">Socket|Endpoint</param>
        protected ServerClientBase(T arg0)
        {
            _arg0 = arg0;
        }

        #endregion

        #region Methods

        internal void SetClientInfo(object info)
        {
            if (!_clientInfo.Equals(info))
            {
                object oldInfo = _clientInfo;
                _clientInfo = info;
                ClientInfoChanged?.Invoke(this, oldInfo, info);
            }
        }

        internal void SetLastReceivedPacketTimeStamp()
        {
            _lastReceivedPacketTimeStamp = DateTime.Now;
        }

        #endregion
    }
}