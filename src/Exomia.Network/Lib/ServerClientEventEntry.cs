#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network.Lib
{
    sealed class ServerClientEventEntry<TServerClient>
        where TServerClient : IServerClient
    {
        internal readonly DeserializePacketHandler<object?>               _deserialize;
        private readonly  Event<ClientDataReceivedHandler<TServerClient>> _dataReceived;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerClientEventEntry{TServerClient}" /> class.
        /// </summary>
        /// <param name="deserialize"> The deserialize. </param>
        public ServerClientEventEntry(DeserializePacketHandler<object?> deserialize)
        {
            _deserialize  = deserialize;
            _dataReceived = new Event<ClientDataReceivedHandler<TServerClient>>();
        }

        /// <summary>
        ///     Adds callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Add(ClientDataReceivedHandler<TServerClient> callback)
        {
            _dataReceived.Add(callback);
        }

        /// <summary>
        ///     Removes the given callback.
        /// </summary>
        /// <param name="callback"> The callback to remove. </param>
        public void Remove(ClientDataReceivedHandler<TServerClient> callback)
        {
            _dataReceived.Remove(callback);
        }

        /// <summary>
        ///     Raises the event entries.
        /// </summary>
        /// <param name="server">     The server. </param>
        /// <param name="client">     The client. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> The responseID. </param>
        public void Raise(IServer<TServerClient> server, TServerClient client, object data, uint responseID)
        {
            for (int i = _dataReceived.Count - 1; i >= 0; --i)
            {
                if (!_dataReceived[i].Invoke(server, client, data, responseID))
                {
                    _dataReceived.Remove(i);
                }
            }
        }
    }
}