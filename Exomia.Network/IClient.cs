#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
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

using System;
using System.Net;
using System.Threading.Tasks;
using Exomia.Network.DefaultPackets;
using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <inheritdoc />
    /// <summary>
    ///     IClient interface
    /// </summary>
    public interface IClient : IDisposable
    {
        /// <summary>
        ///     try's to connect the client to a server
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <returns><b>true</b> if connect was successful; <b>false</b> otherwise</returns>
        bool Connect(string serverAddress, int port, int timeout = 10);

        /// <summary>
        ///     try's to connect the client to a server
        /// </summary>
        /// <param name="ipAddresses"></param>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <returns><b>true</b> if connect was successful; <b>false</b> otherwise</returns>
        bool Connect(IPAddress[] ipAddresses, int port, int timeout = 10);

        /// <summary>
        ///     call to disconnect from a server
        /// </summary>
        void Disconnect();

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length of data</param>
        /// <returns>SendError</returns>
        SendError Send(uint commandid, byte[] data, int offset, int length);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <returns>SendError</returns>
        SendError Send(uint commandid, ISerializable serializable);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">struct data</param>
        /// <returns>SendError</returns>
        SendError Send<T>(uint commandid, in T data) where T : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length of data</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length of data</param>
        /// <param name="deserialize"></param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length,
            DeserializePacketHandler<TResult> deserialize);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length of data</param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length, TimeSpan timeout)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length of data</param>
        /// <param name="deserialize"></param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int length,
            DeserializePacketHandler<TResult> deserialize, TimeSpan timeout);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <param name="deserialize"></param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable,
            DeserializePacketHandler<TResult> deserialize);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable, TimeSpan timeout)
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <param name="deserialize"></param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<TResult>(uint commandid, ISerializable serializable,
            DeserializePacketHandler<TResult> deserialize, TimeSpan timeout);

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">struct data</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">struct data</param>
        /// <param name="deserialize"></param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data, DeserializePacketHandler<TResult> deserialize)
            where T : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">struct data</param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data, TimeSpan timeout)
            where T : unmanaged
            where TResult : unmanaged;

        /// <summary>
        ///     send data to the server
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <typeparam name="TResult">struct type</typeparam>
        /// <param name="commandid">command id</param>
        /// <param name="data">struct data</param>
        /// <param name="deserialize"></param>
        /// <param name="timeout">timeout</param>
        /// <returns>task of Response{TResult}</returns>
        Task<Response<TResult>> SendR<T, TResult>(uint commandid, in T data, DeserializePacketHandler<TResult> deserialize,
            TimeSpan timeout)
            where T : unmanaged;

        /// <summary>
        ///     send a ping command to the server
        /// </summary>
        SendError SendPing();

        /// <summary>
        ///     send a ping command to the server
        /// </summary>
        Task<Response<PingPacket>> SendRPing();
    }
}