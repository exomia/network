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
    class ClientEventEntry
    {
        internal delegate bool DeserializeAndRaiseHandler(in Packet                       packet,
                                                          IClient                         client,
                                                          ushort                          responseID,
                                                          [NotNullWhen(true)] out object? res);

        internal DeserializeAndRaiseHandler _deserializeAndRaise = null!;

        internal static ClientEventEntry Create<T>(DeserializePacketHandler<T> deserialize)
        {
            ClientEventEntry<T> entry = new ClientEventEntry<T>();
            entry._deserializeAndRaise = (in Packet packet, IClient client, ushort responseID, out object? result) =>
            {
                if (deserialize(in packet, out T value))
                {
                    ByteArrayPool.Return(packet.Buffer);
                    entry.Raise(client, value, responseID);

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

    sealed class ClientEventEntry<T> : ClientEventEntry
    {
        private readonly Event<DataReceivedHandler<T>> _dataReceived;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClientEventEntry{T}" /> class.
        /// </summary>
        public ClientEventEntry()
        {
            _dataReceived = new Event<DataReceivedHandler<T>>();
        }

        public void Add(DataReceivedHandler<T> callback)
        {
            _dataReceived.Add(callback);
        }

        public void Remove(DataReceivedHandler<T> callback)
        {
            _dataReceived.Remove(callback);
        }

        public void Raise(IClient client, T data, ushort responseID)
        {
            for (int i = _dataReceived.Count - 1; i >= 0; --i)
            {
                if (!_dataReceived[i].Invoke(client, data, responseID))
                {
                    _dataReceived.Remove(i);
                }
            }
        }
    }
}