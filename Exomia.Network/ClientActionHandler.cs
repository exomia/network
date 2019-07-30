#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     Called than a client action occurs.
    /// </summary>
    /// <typeparam name="TServerClient"> Type of the server client. </typeparam>
    /// <param name="server"> The server. </param>
    /// <param name="client"> The client. </param>
    public delegate void ClientActionHandler<TServerClient>(IServer<TServerClient> server,
                                                            TServerClient          client)
        where TServerClient : IServerClient;
}