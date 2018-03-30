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

        /// <summary>
        ///     ServerClientBase destructor
        /// </summary>
        ~ServerClientBase()
        {
            Dispose(false);
        }

        #endregion

        #region Methods

        /// <summary>
        ///     called than the client info is changed
        /// </summary>
        public event ClientInfoHandler<ServerClientBase<T>, T> ClientInfoChanged;

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

        #region IDisposable Support

        private bool _disposed;

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    /* USER CODE */
                    if (_arg0 is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _arg0 = null;
                    ClientInfoChanged = null;
                }
                _disposed = true;
            }
        }

        /// <summary>
        ///     Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}