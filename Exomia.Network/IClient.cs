#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Net;
using System.Threading.Tasks;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     IClient interface.
    /// </summary>
    public interface IClient : IDisposable
    {
        /// <summary>
        ///     try's to connect the client to a server.
        /// </summary>
        /// <param name="serverAddress"> . </param>
        /// <param name="port">          . </param>
        /// <param name="timeout">       (Optional) </param>
        /// <returns>
        ///     <b>true</b> if connect was successful; <b>false</b> otherwise.
        /// </returns>
        bool Connect(string serverAddress, int port, int timeout = 10);

        /// <summary>
        ///     try's to connect the client to a server.
        /// </summary>
        /// <param name="ipAddresses"> . </param>
        /// <param name="port">        . </param>
        /// <param name="timeout">     (Optional) </param>
        /// <returns>
        ///     <b>true</b> if connect was successful; <b>false</b> otherwise.
        /// </returns>
        bool Connect(IPAddress[] ipAddresses, int port, int timeout = 10);

        /// <summary>
        ///     call to disconnect from a server.
        /// </summary>
        void Disconnect();

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      data. </param>
        /// <param name="offset">    offset. </param>
        /// <param name="length">    length of data. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send(uint commandID, byte[] data, int offset, int length);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send(uint commandID, ISerializable serializable);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      struct data. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send<T>(uint commandID, in T data) where T : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      data. </param>
        /// <param name="offset">    offset. </param>
        /// <param name="length">    length of data. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, int offset, int length)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        data. </param>
        /// <param name="offset">      offset. </param>
        /// <param name="length">      length of data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               int                               offset,
                                               int                               length,
                                               DeserializePacketHandler<TResult> deserialize);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      data. </param>
        /// <param name="offset">    offset. </param>
        /// <param name="length">    length of data. </param>
        /// <param name="timeout">   timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, int offset, int length, TimeSpan timeout)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        data. </param>
        /// <param name="offset">      offset. </param>
        /// <param name="length">      length of data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               int                               offset,
                                               int                               length,
                                               DeserializePacketHandler<TResult> deserialize,
                                               TimeSpan                          timeout);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               ISerializable                     serializable,
                                               DeserializePacketHandler<TResult> deserialize);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="timeout">      timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable, TimeSpan timeout)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <param name="timeout">      timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               ISerializable                     serializable,
                                               DeserializePacketHandler<TResult> deserialize,
                                               TimeSpan                          timeout);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      struct data. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                  in T                              data,
                                                  DeserializePacketHandler<TResult> deserialize)
            where T : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      struct data. </param>
        /// <param name="timeout">   timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data, TimeSpan timeout)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     timeout. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                  in T                              data,
                                                  DeserializePacketHandler<TResult> deserialize,
                                                  TimeSpan                          timeout)
            where T : unmanaged;

        /// <summary>
        ///     send a ping command to the server.
        /// </summary>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendPing();

        /// <summary>
        ///     send a ping command to the server.
        /// </summary>
        /// <returns>
        ///     A Task&lt;Response&lt;PingPacket&gt;&gt;
        /// </returns>
        Task<Response<PingPacket>> SendRPing();
    }
}