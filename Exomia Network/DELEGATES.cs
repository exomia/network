namespace Exomia.Network
{
    /// <summary>
    ///     ClientDataReceivedHandler callback
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    /// <typeparam name="TServerClient">IServer</typeparam>
    /// <param name="server">IServer</param>
    /// <param name="arg0">Socket|EndPoint</param>
    /// <param name="data">object</param>
    /// <returns><b>true</b> if you want to handle more data; <b>false</b> otherwise</returns>
    public delegate bool ClientDataReceivedHandler<T, TServerClient>(ServerBase<T, TServerClient> server, T arg0,
        object data)
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
    public delegate void DisconnectedHandler(IClient client);

    /// <summary>
    ///     ClientActionHandler callback
    /// </summary>
    /// <typeparam name="T">Socket|EndPoint</typeparam>
    /// <param name="arg0"></param>
    public delegate void ClientActionHandler<T>(T arg0) where T : class;

    /// <summary>
    ///     ClientInfoHandler callback
    /// </summary>
    /// <param name="client">ServerClient</param>
    /// <param name="oldValue">oldValue</param>
    /// <param name="newValue">newValue</param>
    public delegate void ClientInfoHandler<T, TArg0>(T client, object oldValue, object newValue)
        where T : ServerClientBase<TArg0>
        where TArg0 : class;
}