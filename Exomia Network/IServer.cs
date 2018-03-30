using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     IServer{T} interface
    /// </summary>
    /// <typeparam name="T">Socket|Endpoint</typeparam>
    internal interface IServer<in T>
        where T : class
    {
        #region Methods

        /// <summary>
        ///     runs the server and starts the listener
        /// </summary>
        /// <param name="port"></param>
        /// <returns><b>true</b> if succesfull; <b>false</b> otherwise</returns>
        bool Run(int port);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="lenght">data lenght</param>
        void SendDataTo(T arg0, uint commandid, uint type, byte[] data, int lenght);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendDataTo(T arg0, uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data async to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendDataToAsync(T arg0, uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <typeparam name="T1">struct type</typeparam>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendDataTo<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data async to the client
        /// </summary>
        /// <typeparam name="T1">struct type</typeparam>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendDataToAsync<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="lenght">data lenght</param>
        void SendToAll(uint commandid, uint type, byte[] data, int lenght);

        /// <summary>
        ///     send data async to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="lenght">data lenght</param>
        void SendToAllAsync(uint commandid, uint type, byte[] data, int lenght);

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendToAll<T1>(uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data async to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendToAllAsync<T1>(uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendToAll(uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data async to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendToAllAsync(uint commandid, uint type, ISerializable serializable);

        #endregion
    }
}