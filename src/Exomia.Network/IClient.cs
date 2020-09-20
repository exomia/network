#region License

// Copyright (c) 2018-2020, exomia
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
        /// <param name="serverAddress">      The server address. </param>
        /// <param name="port">               The port. </param>
        /// <param name="overwriteConfigure"> (Optional) Overwrite the default configuration. </param>
        /// <param name="timeout">            (Optional) The timeout in seconds. </param>
        /// <returns>
        ///     <b>true</b> if connect was successful; <b>false</b> otherwise.
        /// </returns>
        bool Connect(string serverAddress, int port, Action<ClientBase>? overwriteConfigure = null, int timeout = 10);

        /// <summary>
        ///     try's to connect the client to a server.
        /// </summary>
        /// <param name="ipAddresses">        The ip addresses. </param>
        /// <param name="port">               The port. </param>
        /// <param name="overwriteConfigure"> (Optional) Overwrite the default configuration. </param>
        /// <param name="timeout">            (Optional) The timeout in seconds. </param>
        /// <returns>
        ///     <b>true</b> if connect was successful; <b>false</b> otherwise.
        /// </returns>
        bool Connect(IPAddress[]         ipAddresses,
                     int                 port,
                     Action<ClientBase>? overwriteConfigure = null,
                     int                 timeout            = 10);

        /// <summary>
        ///     call to disconnect from a server.
        /// </summary>
        void Disconnect();

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length of data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send(uint commandID, byte[] data, int offset, int length, uint responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send(uint commandID, byte[] data, uint responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send(uint commandID, ISerializable serializable, uint responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The struct data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     SendError.
        /// </returns>
        SendError Send<T>(uint commandID, in T data, uint responseID = 0) where T : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length of data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, int offset, int length, uint responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, uint responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="offset">      The offset. </param>
        /// <param name="length">      The length of data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               int                               offset,
                                               int                               length,
                                               DeserializePacketHandler<TResult> deserialize,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               DeserializePacketHandler<TResult> deserialize,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="offset">     The offset. </param>
        /// <param name="length">     The length of data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint     commandID,
                                               byte[]   data,
                                               int      offset,
                                               int      length,
                                               TimeSpan timeout,
                                               uint     responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, byte[] data, TimeSpan timeout, uint responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
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
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               int                               offset,
                                               int                               length,
                                               DeserializePacketHandler<TResult> deserialize,
                                               TimeSpan                          timeout,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     The timeout. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               byte[]                            data,
                                               DeserializePacketHandler<TResult> deserialize,
                                               TimeSpan                          timeout,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint commandID, ISerializable serializable, uint responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               ISerializable                     serializable,
                                               DeserializePacketHandler<TResult> deserialize,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="timeout">      The timeout. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint          commandID,
                                               ISerializable serializable,
                                               TimeSpan      timeout,
                                               uint          responseID = 0)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">    Identifier for the command. </param>
        /// <param name="serializable"> ISerializable. </param>
        /// <param name="deserialize">  The deserialize. </param>
        /// <param name="timeout">      The timeout. </param>
        /// <param name="responseID">   (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<TResult>(uint                              commandID,
                                               ISerializable                     serializable,
                                               DeserializePacketHandler<TResult> deserialize,
                                               TimeSpan                          timeout,
                                               uint                              responseID = 0);

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The struct data. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data, uint responseID = 0)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                  in T                              data,
                                                  DeserializePacketHandler<TResult> deserialize,
                                                  uint                              responseID = 0)
            where T : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">  Identifier for the command. </param>
        /// <param name="data">       The struct data. </param>
        /// <param name="timeout">    The timeout. </param>
        /// <param name="responseID"> (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandID, in T data, TimeSpan timeout, uint responseID = 0)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server.
        /// </summary>
        /// <typeparam name="T">       struct type. </typeparam>
        /// <typeparam name="TResult"> struct type. </typeparam>
        /// <param name="commandID">   Identifier for the command. </param>
        /// <param name="data">        The struct data. </param>
        /// <param name="deserialize"> The deserialize. </param>
        /// <param name="timeout">     The timeout. </param>
        /// <param name="responseID">  (Optional) Identifier for the response. </param>
        /// <returns>
        ///     task of Response{TResult}
        /// </returns>
        Task<Response<TResult>> SendR<T, TResult>(uint                              commandID,
                                                  in T                              data,
                                                  DeserializePacketHandler<TResult> deserialize,
                                                  TimeSpan                          timeout,
                                                  uint                              responseID = 0)
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