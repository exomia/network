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
    ///     ClientDataReceivedHandler callback.
    /// </summary>
    /// <typeparam name="T">             Socket|EndPoint. </typeparam>
    /// <typeparam name="TServerClient"> TServerClient. </typeparam>
    /// <param name="server">     IServer. </param>
    /// <param name="arg0">       Socket|EndPoint. </param>
    /// <param name="data">       object. </param>
    /// <param name="responseid"> responseid. </param>
    /// <param name="client">     TServerClient. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool ClientDataReceivedHandler<T, TServerClient>(ServerBase<T, TServerClient> server, T arg0,
                                                                     object                       data,
                                                                     uint                         responseid,
                                                                     TServerClient                client)
        where T : class
        where TServerClient : ServerClientBase<T>;
}