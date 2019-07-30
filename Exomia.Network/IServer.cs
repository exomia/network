#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     Interface for server.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    public interface IServer<in TServerClient> : IDisposable
        where TServerClient : IServerClient
    {
        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseID"> Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandID, byte[] data, int offset, int length, uint responseID);

        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> The serializable. </param>
        /// <param name="responseID">   Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandID, ISerializable serializable, uint responseID);

        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo<T1>(TServerClient client, uint commandID, in T1 data, uint responseID) where T1 : unmanaged;

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      The data. </param>
        /// <param name="offset">    The offset. </param>
        /// <param name="length">    The length. </param>
        void SendToAll(uint commandID, byte[] data, int offset, int length);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> The serializable. </param>
        void SendToAll(uint commandID, ISerializable serializable);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      The data. </param>
        void SendToAll<T1>(uint commandID, in T1 data) where T1 : unmanaged;
    }
}