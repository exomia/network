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
        ///     send data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        /// <param name="lenght">data lenght</param>
        void SendTo(T arg0, uint commandid, uint type, byte[] data, int lenght);

        /// <summary>
        ///     send data as a response to the client
        /// </summary>
        /// <param name="arg0"></param>
        /// <param name="data"></param>
        /// <param name="lenght"></param>
        /// <param name="responseid"></param>
        void SendResponseTo(T arg0, byte[] data, int lenght, uint responseid);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendTo(T arg0, uint commandid, uint type, ISerializable serializable);

        /// <summary>
        ///     send data async to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="serializable">ISerializable</param>
        void SendToAsync(T arg0, uint commandid, uint type, ISerializable serializable);

        /// <summary>
        /// send data as a response to the client
        /// </summary>
        /// <param name="arg0"></param>
        /// <param name="serializable"></param>
        /// <param name="responseid"></param>
        void SendResponseTo(T arg0, ISerializable serializable, uint responseid);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <typeparam name="T1">struct type</typeparam>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendTo<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data async to the client
        /// </summary>
        /// <typeparam name="T1">struct type</typeparam>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="type">type</param>
        /// <param name="data">data</param>
        void SendToAsync<T1>(T arg0, uint commandid, uint type, in T1 data) where T1 : struct;

        /// <summary>
        ///     send data as a response to the client
        /// </summary>
        /// <param name="arg0"></param>
        /// <param name="data"></param>
        /// <param name="responseid"></param>
        void SendResponseTo<T1>(T arg0, in T1 data, uint responseid) where T1 : struct;

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