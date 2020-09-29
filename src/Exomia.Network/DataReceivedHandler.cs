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
    ///     Called than a client received data from the server.
    /// </summary>
    /// <param name="client">     The client. </param>
    /// <param name="commandID">  Identifier for the command. </param>
    /// <param name="data">       The data. </param>
    /// <param name="responseID"> Identifier for the response. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool CommandDataReceivedHandler(IClient client, ushort commandID, object data, ushort responseID);

    /// <summary>
    ///     Called than a client received data from the server.
    /// </summary>
    /// <param name="client">     The client. </param>
    /// <param name="data">       The data. </param>
    /// <param name="responseID"> Identifier for the response. </param>
    /// <returns>
    ///     <b>true</b> if you want to handle more data; <b>false</b> otherwise.
    /// </returns>
    public delegate bool DataReceivedHandler(IClient client, object data, ushort responseID);
}