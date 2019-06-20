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
    ///     DataReceivedHandler callback.
    /// </summary>
    /// <param name="client"> IClient. </param>
    /// <param name="data">   data. </param>
    /// <returns>
    ///     A bool.
    /// </returns>
    public delegate bool DataReceivedHandler(IClient client, object data);
}