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