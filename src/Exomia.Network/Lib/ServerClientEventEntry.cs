#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Diagnostics.CodeAnalysis;
using Exomia.Network.Buffers;

namespace Exomia.Network.Lib
{
    class ServerClientEventEntry<TServerClient>
        where TServerClient : class, IServerClient
    {
        internal delegate bool DeserializeAndRaiseHandler(in Packet                       packet,
                                                          IServer<TServerClient>          server,
                                                          TServerClient                   client,
                                                          ushort                          responseID,
                                                          [NotNullWhen(true)] out object? res);
        internal DeserializeAndRaiseHandler _deserializeAndRaise = null!;

        internal static ServerClientEventEntry<TServerClient> Create<T>(DeserializePacketHandler<T> deserialize)
        {
            ServerClientEventEntry<TServerClient, T> entry = new ServerClientEventEntry<TServerClient, T>();
            entry._deserializeAndRaise = (in Packet              packet,
                                          IServer<TServerClient> server,
                                          TServerClient          client,
                                          ushort                 responseID,
                                          out object?            result) =>
            {
                if (deserialize(in packet, out T value))
                {
                    ByteArrayPool.Return(packet.Buffer);
                    entry.Raise(server, client, value, responseID);

                    // ReSharper disable once HeapView.PossibleBoxingAllocation
                    result = value;
                    return true;
                }

                ByteArrayPool.Return(packet.Buffer);
                result = null;
                return false;
            };

            return entry;
        }
    }

    sealed class ServerClientEventEntry<TServerClient, T> : ServerClientEventEntry<TServerClient>
        where TServerClient : class, IServerClient
    {
        private readonly Event<ClientDataReceivedHandler<TServerClient, T>> _dataReceived;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientEventEntry" /> class.
        /// </summary>
        public ServerClientEventEntry()
        {
            _dataReceived = new Event<ClientDataReceivedHandler<TServerClient, T>>();
        }

        public void Add(ClientDataReceivedHandler<TServerClient, T> callback)
        {
            _dataReceived.Add(callback);
        }

        public void Remove(ClientDataReceivedHandler<TServerClient, T> callback)
        {
            _dataReceived.Remove(callback);
        }

        public void Raise(IServer<TServerClient> server, TServerClient client, T data, ushort responseID)
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