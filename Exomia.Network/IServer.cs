#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     IServer{T} interface.
    /// </summary>
    /// <typeparam name="T"> Socket|Endpoint. </typeparam>
    interface IServer<in T>
        where T : class
    {
        /// <summary>
        ///     runs the server and starts the listener.
        /// </summary>
        /// <param name="port"> . </param>
        /// <returns>
        ///     <b>true</b> if successful; <b>false</b> otherwise.
        /// </returns>
        bool Run(int port);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="arg0">       Socket|EndPoint. </param>
        /// <param name="commandid">  command id. </param>
        /// <param name="data">       data. </param>
        /// <param name="offset">     offset. </param>
        /// <param name="length">     data length. </param>
        /// <param name="responseid"> . </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError SendTo(T arg0, uint commandid, byte[] data, int offset, int length, uint responseid);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="arg0">         Socket|EndPoint. </param>
        /// <param name="commandid">    command id. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="responseid">   . </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError SendTo(T arg0, uint commandid, ISerializable serializable, uint responseid);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T1"> struct type. </typeparam>
        /// <param name="arg0">       Socket|EndPoint. </param>
        /// <param name="commandid">  command id. </param>
        /// <param name="data">       data. </param>
        /// <param name="responseid"> . </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError SendTo<T1>(T arg0, uint commandid, in T1 data, uint responseid) where T1 : unmanaged;

        /// <summary>
        ///     send data to all clients.
        /// </summary>
        /// <param name="commandid"> command id. </param>
        /// <param name="data">      data. </param>
        /// <param name="offset">    offset. </param>
        /// <param name="length">    data length. </param>
        void SendToAll(uint commandid, byte[] data, int offset, int length);

        /// <summary>
        ///     send data to all clients.
        /// </summary>
        /// <param name="commandid">    command id. </param>
        /// <param name="serializable"> ISerializable. </param>
        void SendToAll(uint commandid, ISerializable serializable);

        /// <summary>
        ///     send data to all clients.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="commandid"> command id. </param>
        /// <param name="data">      data. </param>
        void SendToAll<T1>(uint commandid, in T1 data) where T1 : unmanaged;
    }
}