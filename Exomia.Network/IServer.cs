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
    /// <typeparam name="T">             Generic type parameter. </typeparam>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    interface IServer<in T, in TServerClient> : IDisposable
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        /// <summary>
        ///     Runs.
        /// </summary>
        /// <param name="port"> The port. </param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        bool Run(int port);

        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <param name="client">     The client. </param>
        /// <param name="commandid">  The commandid. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseid"> The responseid. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandid, byte[] data, int offset, int length, uint responseid);

        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <param name="client">       The client. </param>
        /// <param name="commandid">    The commandid. </param>
        /// <param name="serializable"> The serializable. </param>
        /// <param name="responseid">   The responseid. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandid, ISerializable serializable, uint responseid);

        /// <summary>
        ///     Sends data to the client.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandid">  The commandid. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseid"> The responseid. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo<T1>(TServerClient client, uint commandid, in T1 data, uint responseid) where T1 : unmanaged;

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandid"> The commandid. </param>
        /// <param name="data">      The data. </param>
        /// <param name="offset">    The offset. </param>
        /// <param name="length">    The length. </param>
        void SendToAll(uint commandid, byte[] data, int offset, int length);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandid">    The commandid. </param>
        /// <param name="serializable"> The serializable. </param>
        void SendToAll(uint commandid, ISerializable serializable);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="commandid"> The commandid. </param>
        /// <param name="data">      The data. </param>
        void SendToAll<T1>(uint commandid, in T1 data) where T1 : unmanaged;
    }
}