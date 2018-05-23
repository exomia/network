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

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Exomia.Network.Buffers;
using Exomia.Network.Serialization;

namespace Exomia.Network.UDP
{
    /// <inheritdoc />
    public abstract class UdpClientBase : ClientBase
    {
        #region Variables

        private readonly byte[] _connectChecksum = new byte[16];

        private readonly ManualResetEvent _manuelResetEvent;

        private readonly ClientStateObject _state;

        #endregion

        #region Constructors

        /// <inheritdoc />
        protected UdpClientBase()
        {
            _state = new ClientStateObject { Buffer = new byte[Constants.PACKET_SIZE_MAX] };

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);

            AddDataReceivedCallback(
                Constants.UDP_CONNECT_COMMAND_ID, (_, data) =>
                {
                    UDP_CONNECT_STRUCT connectStruct = (UDP_CONNECT_STRUCT)data;
                    if (connectStruct.Checksum.SequenceEqual(_connectChecksum))
                    {
                        _manuelResetEvent.Set();
                    }
                    return true;
                });
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override bool OnConnect(string serverAddress, int port, int timeout, out Socket socket)
        {
            _manuelResetEvent.Reset();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Connect(Dns.GetHostAddresses(serverAddress), port);
                ClientReceiveDataAsync();
                SendConnect();

                return _manuelResetEvent.WaitOne(timeout * 1000);
            }
            catch
            {
                /* IGNORE */
            }

            socket = null;
            return false;
        }

        private void ClientReceiveDataAsync()
        {
            try
            {
                _clientSocket.BeginReceive(
                    _state.Buffer, 0, Constants.PACKET_SIZE_MAX, SocketFlags.None, ClientReceiveDataCallback, null);
            }
            catch { OnDisconnected(); }
        }

        private void ClientReceiveDataCallback(IAsyncResult iar)
        {
            int length;
            try
            {
                if ((length = _clientSocket.EndReceive(iar)) <= 0)
                {
                    OnDisconnected();
                    return;
                }
            }
            catch
            {
                OnDisconnected();
                return;
            }

            _state.Buffer.GetHeader(out uint commandID, out int dataLength, out uint response);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                byte[] data;
                uint responseID = 0;
                if (response != 0)
                {
                    responseID = BitConverter.ToUInt32(_state.Buffer, Constants.HEADER_SIZE);

                    dataLength -= Constants.RESPONSE_SIZE;
                    data = ByteArrayPool.Rent(dataLength);
                    Buffer.BlockCopy(
                        _state.Buffer, Constants.HEADER_SIZE + Constants.RESPONSE_SIZE, data, 0, dataLength);
                }
                else
                {
                    data = ByteArrayPool.Rent(dataLength);
                    Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                }
                DeserializeDataAsync(commandID, data, 0, dataLength, responseID);
                ByteArrayPool.Return(data);
            }

            ClientReceiveDataAsync();
        }

        private void SendConnect()
        {
            Send(Constants.UDP_CONNECT_COMMAND_ID, new UDP_CONNECT_STRUCT { Checksum = _connectChecksum });
        }

        #endregion

        #region Nested

        private struct ClientStateObject
        {
            public byte[] Buffer;
        }

        #endregion
    }
}