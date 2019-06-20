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
    ///     ClientDisconnectHandler callback.
    /// </summary>
    /// <typeparam name="T"> Socket|EndPoint. </typeparam>
    /// <param name="arg0">   The args. </param>
    /// <param name="reason"> The reason. </param>
    public delegate void ClientDisconnectHandler<in T>(T arg0, DisconnectReason reason) where T : class;
}