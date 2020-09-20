#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Threading.Tasks;
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
        ///     send data to the client.
        /// </summary>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client,
                         uint          commandID,
                         byte[]        data,
                         int           offset,
                         int           length,
                         uint          responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandID, byte[] data, uint responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> The serializable. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, uint commandID, ISerializable serializable, uint responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo<T1>(TServerClient client, uint commandID, in T1 data, uint responseID = 0)
            where T1 : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length of data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 uint          commandID,
                                                 byte[]        data,
                                                 int           offset,
                                                 int           length,
                                                 uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client, uint commandID, byte[] data, uint responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="offset">      The offset. </param>
        /// <param name="length">      The length of data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 byte[]                            data,
                                                 int                               offset,
                                                 int                               length,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 byte[]                            data,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length of data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 uint          commandID,
                                                 byte[]        data,
                                                 int           offset,
                                                 int           length,
                                                 TimeSpan      timeout,
                                                 uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 uint          commandID,
                                                 byte[]        data,
                                                 TimeSpan      timeout,
                                                 uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="offset">      The offset. </param>
        /// <param name="length">      The length of data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     The timeout. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 byte[]                            data,
                                                 int                               offset,
                                                 int                               length,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     The timeout. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 byte[]                            data,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 uint          commandID,
                                                 ISerializable serializable,
                                                 uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 ISerializable                     serializable,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="timeout">      The timeout. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 uint          commandID,
                                                 ISerializable serializable,
                                                 TimeSpan      timeout,
                                                 uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">       The client. </param>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <param name="timeout">      The timeout. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 uint                              commandID,
                                                 ISerializable                     serializable,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 uint                              responseID = 0);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The struct data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient client,
                                                    uint          commandID,
                                                    in T          data,
                                                    uint          responseID = 0)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient                     client,
                                                    uint                              commandID,
                                                    in T                              data,
                                                    DeserializePacketHandler<TResult> deserialize,
                                                    uint                              responseID = 0)
            where T : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">     The client. </param>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The struct data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient client,
                                                    uint          commandID,
                                                    in T          data,
                                                    TimeSpan      timeout,
                                                    uint          responseID = 0)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">      The client. </param>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     The timeout. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient                     client,
                                                    uint                              commandID,
                                                    in T                              data,
                                                    DeserializePacketHandler<TResult> deserialize,
                                                    TimeSpan                          timeout,
                                                    uint                              responseID = 0)
            where T : unmanaged;

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
        /// <param name="commandID"> Identifier for the command. </param>
        /// <param name="data">      The data. </param>
        void SendToAll(uint commandID, byte[] data);

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