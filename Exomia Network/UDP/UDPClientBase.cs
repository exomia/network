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
using Exomia.Network.Extensions.Struct;
using Exomia.Network.Serialization;
using LZ4;

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
        protected UdpClientBase(int maxPacketSize = 0)
        {
            _state = new ClientStateObject
                { Buffer = new byte[maxPacketSize > 0 ? maxPacketSize : Constants.PACKET_SIZE_MAX] };

            Random rnd = new Random((int)DateTime.UtcNow.Ticks);
            rnd.NextBytes(_connectChecksum);

            _manuelResetEvent = new ManualResetEvent(false);
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

        /// <inheritdoc />
        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                Send(CommandID.UDP_DISCONNECT, new byte[1] { 255 }, 0, 1);
            }
        }

        /// <inheritdoc />
        internal override void OnDefaultCommand(uint commandid, byte[] data, int offset, int length)
        {
            switch (commandid)
            {
                case CommandID.UDP_CONNECT:
                {
                    data.FromBytesUnsafe(offset, out UDP_CONNECT_STRUCT connectStruct);
                    if (connectStruct.Checksum.SequenceEqual(_connectChecksum))
                    {
                        _manuelResetEvent.Set();
                    }
                    break;
                }
            }
        }

        private void ClientReceiveDataAsync()
        {
            try
            {
                _clientSocket.BeginReceive(
                    _state.Buffer, 0, _state.Buffer.Length, SocketFlags.None, ClientReceiveDataCallback, null);
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

            _state.Buffer.GetHeader(out uint commandID, out int dataLength, out uint response, out uint compressed);

            if (dataLength == length - Constants.HEADER_SIZE)
            {
                uint responseID = 0;
                byte[] data;
                if (compressed != 0)
                {
                    int l;
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(_state.Buffer, Constants.HEADER_SIZE);
                        l = BitConverter.ToInt32(_state.Buffer, Constants.HEADER_SIZE + 4);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _state.Buffer, Constants.HEADER_SIZE + 8, dataLength - 8, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }
                    else
                    {
                        l = BitConverter.ToInt32(_state.Buffer, 0);
                        data = ByteArrayPool.Rent(l);

                        int s = LZ4Codec.Decode(
                            _state.Buffer, Constants.HEADER_SIZE + 4, dataLength - 4, data, 0, l, true);
                        if (s != l) { throw new Exception("LZ4.Decode FAILED!"); }
                    }

                    ClientReceiveDataAsync();

                    DeserializeData(commandID, data, 0, l, responseID);
                }
                else
                {
                    if (response != 0)
                    {
                        responseID = BitConverter.ToUInt32(_state.Buffer, Constants.HEADER_SIZE);
                        dataLength -= 4;
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE + 4, data, 0, dataLength);
                    }
                    else
                    {
                        data = ByteArrayPool.Rent(dataLength);
                        Buffer.BlockCopy(_state.Buffer, Constants.HEADER_SIZE, data, 0, dataLength);
                    }

                    ClientReceiveDataAsync();

                    DeserializeData(commandID, data, 0, dataLength, responseID);
                }
                return;
            }

            ClientReceiveDataAsync();
        }

        private void SendConnect()
        {
            Send(CommandID.UDP_CONNECT, new UDP_CONNECT_STRUCT { Checksum = _connectChecksum });
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