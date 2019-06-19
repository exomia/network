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
    ///     A client event entry. This class cannot be inherited.
    /// </summary>
    sealed class ClientEventEntry
    {
        /// <summary>
        ///     The deserialize.
        /// </summary>
        internal readonly DeserializePacketHandler<object> _deserialize;

        /// <summary>
        ///     The data received.
        /// </summary>
        private readonly Event<DataReceivedHandler> _dataReceived;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientEventEntry" /> class.
        /// </summary>
        /// <param name="deserialize"> The deserialize. </param>
        public ClientEventEntry(DeserializePacketHandler<object> deserialize)
        {
            _dataReceived = new Event<DataReceivedHandler>();
            _deserialize  = deserialize;
        }

        /// <summary>
        ///     Adds callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Add(DataReceivedHandler callback)
        {
            _dataReceived.Add(callback);
        }

        /// <summary>
        ///     Removes the given callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Remove(DataReceivedHandler callback)
        {
            _dataReceived.Remove(callback);
        }

        /// <summary>
        ///     Raises.
        /// </summary>
        /// <param name="client"> The client. </param>
        /// <param name="result"> The result. </param>
        public void Raise(IClient client, object result)
        {
            for (int i = _dataReceived.Count - 1; i >= 0; --i)
            {
                if (!_dataReceived[i].Invoke(client, result))
                {
                    _dataReceived.Remove(i);
                }
            }
        }
    }
}