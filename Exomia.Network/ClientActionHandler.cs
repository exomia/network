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
    ///     ClientActionHandler callback.
    /// </summary>
    /// <typeparam name="T"> Socket|EndPoint. </typeparam>
    /// <param name="arg0"> . </param>
    public delegate void ClientActionHandler<in T>(T arg0) where T : class;
}