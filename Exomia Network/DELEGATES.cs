#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     ClientDataReceivedHandler callback
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    /// <typeparam name="TServerClient">TServerClient</typeparam>
    /// <param name="server">IServer</param>
    /// <param name="arg0">Socket|EndPoint</param>
    /// <param name="data">object</param>
    /// <param name="responseid">responseid</param>
    /// <param name="client">TServerClient</param>
    /// <returns><b>true</b> if you want to handle more data; <b>false</b> otherwise</returns>
    public delegate bool ClientDataReceivedHandler<T, TServerClient>(ServerBase<T, TServerClient> server, T arg0,
        object data, uint responseid, TServerClient client)
        where T : class
        where TServerClient : ServerClientBase<T>;

    /// <summary>
    ///     DataReceivedHandler callback
    /// </summary>
    /// <param name="client">IClient</param>
    /// <param name="data">data</param>
    /// <returns></returns>
    public delegate bool DataReceivedHandler(IClient client, object data);

    /// <summary>
    ///     DisconnectedHandler callback
    /// </summary>
    /// <param name="client">client</param>
    /// <param name="reason">reason</param>
    public delegate void DisconnectedHandler(IClient client, DisconnectReason reason);

    /// <summary>
    ///     ClientActionHandler callback
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    /// <param name="arg0"></param>
    public delegate void ClientActionHandler<in T>(T arg0) where T : class;

    /// <summary>
    ///     ClientDisconnectHandler callback
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    /// <param name="arg0"></param>
    /// <param name="reason">reason</param>
    public delegate void ClientDisconnectHandler<in T>(T arg0, DisconnectReason reason) where T : class;

    /// <summary>
    ///     ClientInfoHandler callback
    /// </summary>
    /// <param name="client">ServerClient</param>
    /// <param name="oldValue">oldValue</param>
    /// <param name="newValue">newValue</param>
    public delegate void ClientInfoHandler<in T, TArg0>(T client, object oldValue, object newValue)
        where T : ServerClientBase<TArg0>
        where TArg0 : class;

    /// <summary>
    ///     DeserializePacket callback
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="packet"></param>
    /// <returns></returns>
    public delegate TResult DeserializePacket<out TResult>(in Packet packet);
}