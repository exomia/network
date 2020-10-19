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
        where TServerClient : class, IServerClient
    {
        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client,
                         ushort        commandOrResponseID,
                         byte[]        data,
                         int           offset,
                         int           length,
                         bool          isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client, ushort commandOrResponseID, byte[] data, bool isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        The serializable. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo(TServerClient client,
                         ushort        commandOrResponseID,
                         ISerializable serializable,
                         bool          isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     A SendError.
        /// </returns>
        SendError SendTo<T1>(TServerClient client, ushort commandOrResponseID, in T1 data, bool isResponse = false)
            where T1 : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length of data. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 byte[]        data,
                                                 int           offset,
                                                 int           length,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 byte[]        data,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length of data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 byte[]                            data,
                                                 int                               offset,
                                                 int                               length,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 byte[]                            data,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length of data. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 byte[]        data,
                                                 int           offset,
                                                 int           length,
                                                 TimeSpan      timeout,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 byte[]        data,
                                                 TimeSpan      timeout,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length of data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 byte[]                            data,
                                                 int                               offset,
                                                 int                               length,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 byte[]                            data,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        ISerializable. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 ISerializable serializable,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        ISerializable. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 ISerializable                     serializable,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        ISerializable. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient client,
                                                 ushort        commandOrResponseID,
                                                 ISerializable serializable,
                                                 TimeSpan      timeout,
                                                 bool          isResponse = false)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        ISerializable. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<TResult>(TServerClient                     client,
                                                 ushort                            commandOrResponseID,
                                                 ISerializable                     serializable,
                                                 DeserializePacketHandler<TResult> deserialize,
                                                 TimeSpan                          timeout,
                                                 bool                              isResponse = false);

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The struct data. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient client,
                                                    ushort        commandOrResponseID,
                                                    in T          data,
                                                    bool          isResponse = false)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The struct data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient                     client,
                                                    ushort                            commandOrResponseID,
                                                    in T                              data,
                                                    DeserializePacketHandler<TResult> deserialize,
                                                    bool                              isResponse = false)
            where T : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The struct data. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient client,
                                                    ushort        commandOrResponseID,
                                                    in T          data,
                                                    TimeSpan      timeout,
                                                    bool          isResponse = false)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the client.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="client">              The client. </param>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The struct data. </param>
        /// <param name="deserialize">         The deserialize. </param>
        /// <param name="timeout">             The timeout. </param>
        /// <param name="isResponse">          (Optional) True if this object is response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendToR<T, TResult>(TServerClient                     client,
                                                    ushort                            commandOrResponseID,
                                                    in T                              data,
                                                    DeserializePacketHandler<TResult> deserialize,
                                                    TimeSpan                          timeout,
                                                    bool                              isResponse = false)
            where T : unmanaged;

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="offset">              The offset. </param>
        /// <param name="length">              The length. </param>
        /// <param name="exclude">             (Optional) The excluded client. </param>
        void SendToAll(ushort commandOrResponseID, byte[] data, int offset, int length, TServerClient? exclude = null);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="exclude">             (Optional) The excluded client. </param>
        void SendToAll(ushort commandOrResponseID, byte[] data, TServerClient? exclude = null);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="serializable">        The serializable. </param>
        /// <param name="exclude">             (Optional) The excluded client. </param>
        void SendToAll(ushort commandOrResponseID, ISerializable serializable, TServerClient? exclude = null);

        /// <summary>
        ///     Sends data to all clients.
        /// </summary>
        /// <typeparam name="T1"> Generic type parameter. </typeparam>
        /// <param name="commandOrResponseID"> Identifier for the command or response. </param>
        /// <param name="data">                The data. </param>
        /// <param name="exclude">             (Optional) The excluded client. </param>
        void SendToAll<T1>(ushort commandOrResponseID, in T1 data, TServerClient? exclude = null) where T1 : unmanaged;
    }
}