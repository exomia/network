using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     IClient interface
    /// </summary>
    public interface IClient
    {
        #region Methods

        /// <summary>
        ///     trys to connect the client to a server
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <returns><b>true</b> if connect was succesfull; <b>false</b> otherwise</returns>
        bool Connect(string serverAddress, int port, int timeout = 10);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="lenght">lenght of data</param>
        void SendData(uint commandid, uint type, byte[] data, int lenght);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendData(uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data async to the server
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendDataAsync(uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendData<T>(uint commandid, uint type, in T data) where T : struct;

        /// <summary>
        ///     send data async to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendDataAsync<T>(uint commandid, uint type, in T data) where T : struct;

        /// <summary>
        ///     send a ping command to the server
        /// </summary>
        void SendPing();

        /// <summary>
        ///     send a client info command to the server
        /// </summary>
        /// <param name="clientID">client id</param>
        /// <param name="clientName">client name</param>
        void SendClientInfo(long clientID, string clientName);

        #endregion
    }
}