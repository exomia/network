#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

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