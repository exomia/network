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

namespace Exomia.Network.Lib
{
    /// <summary>
    ///     A server client event entry. This class cannot be inherited.
    /// </summary>
    /// <typeparam name="T">             Generic type parameter. </typeparam>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    sealed class ServerClientEventEntry<T, TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        /// <summary>
        ///     The deserialize.
        /// </summary>
        internal readonly DeserializePacketHandler<object> _deserialize;

        /// <summary>
        ///     The data received.
        /// </summary>
        private readonly Event<ClientDataReceivedHandler<T, TServerClient>> _dataReceived;

        /// <summary>
        ///     Initializes a new instance of the &lt;see cref="ServerClientEventEntry&lt;T,
        ///     TServerClient&gt;"/&gt; class.
        /// </summary>
        /// <param name="deserialize"> The deserialize. </param>
        public ServerClientEventEntry(DeserializePacketHandler<object> deserialize)
        {
            _dataReceived = new Event<ClientDataReceivedHandler<T, TServerClient>>();
            _deserialize  = deserialize;
        }

        /// <summary>
        ///     Adds callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Add(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived.Add(callback);
        }

        /// <summary>
        ///     Removes the given callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Remove(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived.Remove(callback);
        }

        /// <summary>
        ///     Raises the event entries.
        /// </summary>
        /// <param name="server">     The server. </param>
        /// <param name="arg0">       The argument 0. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseid"> The responseid. </param>
        /// <param name="client">     The client. </param>
        public void Raise(ServerBase<T, TServerClient> server, T arg0, object data, uint responseid,
                          TServerClient                client)
        {
            for (int i = _dataReceived.Count - 1; i >= 0; --i)
            {
                if (!_dataReceived[i].Invoke(server, arg0, data, responseid, client))
                {
                    _dataReceived.Remove(i);
                }
            }
        }
    }
}