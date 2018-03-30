using System.Net.Sockets;

namespace Exomia.Network.Extensions.Socket
{
    /// <summary>
    ///     SocketExt class
    /// </summary>
    internal static class SocketExt
    {
        #region Methods

        /// <summary>
        ///     checks if a socket is connected
        /// </summary>
        /// <param name="socket">Socket</param>
        /// <returns><b>true</b> if connected; <b>false</b> otherwise</returns>
        internal static bool IsConnected(this System.Net.Sockets.Socket socket)
        {
            try
            {
                if (socket != null && !socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                {
                    return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        ///     checks if a socket has data available
        /// </summary>
        /// <param name="socket">Socket</param>
        /// <returns><b>true</b> if data is available; <b>false</b> otherwise</returns>
        internal static bool IsDataAvailable(this System.Net.Sockets.Socket socket)
        {
            try
            {
                if (socket != null && !socket.Poll(0, SelectMode.SelectRead) && !socket.Poll(0, SelectMode.SelectError))
                {
                    byte[] buffer = new byte[1];
                    if (socket.Receive(buffer, SocketFlags.Peek) != 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                /* IGNORE */
            }
            return false;
        }

        #endregion
    }
}