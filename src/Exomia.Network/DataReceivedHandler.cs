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
    ///     Called than a client received data from the server.
    /// </summary>
    /// <param name="client">    The client. </param>
    /// <param name="commandID"> Identifier for the command. </param>
    /// <param name="data">      The data. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool CommandDataReceivedHandler(IClient client, uint commandID, object data);

    /// <summary>
    ///     Called than a client received data from the server.
    /// </summary>
    /// <param name="client"> The client. </param>
    /// <param name="data">   The data. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool DataReceivedHandler(IClient client, object data);
}