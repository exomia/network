#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     Handler, called when the server received client data.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    /// <param name="server">     The server. </param>
    /// <param name="client">     The client. </param>
    /// <param name="commandID">  Identifier for the command. </param>
    /// <param name="data">       The data. </param>
    /// <param name="responseID"> Identifier for the response. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool ClientCommandDataReceivedHandler<TServerClient>(IServer<TServerClient> server,
                                                                         TServerClient          client,
                                                                         uint                   commandID,
                                                                         object                 data,
                                                                         uint                   responseID)
        where TServerClient : IServerClient;

    /// <summary>
    ///     Handler, called when the server received client data.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    /// <param name="server">     The server. </param>
    /// <param name="client">     The client. </param>
    /// <param name="data">       The data. </param>
    /// <param name="responseID"> The responseID. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool ClientDataReceivedHandler<TServerClient>(IServer<TServerClient> server,
                                                                  TServerClient          client,
                                                                  object                 data,
                                                                  uint                   responseID)
        where TServerClient : IServerClient;
}