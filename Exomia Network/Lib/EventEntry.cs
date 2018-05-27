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
using System.Runtime.Remoting.Messaging;

namespace Exomia.Network.Lib
{
    internal sealed class ServerClientEventEntry<T, TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        #region Variables

        private event ClientDataReceivedHandler<T, TServerClient> _dataReceived;

        internal readonly DeserializeData _deserialize;

        #endregion

        #region Constructors

        public ServerClientEventEntry(DeserializeData deserialize)
        {
            _deserialize = deserialize;
        }

        #endregion

        #region Methods

        public void Add(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived += callback;
        }

        public void Remove(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived -= callback;
        }

        public void RaiseAsync(ServerBase<T, TServerClient> server, T arg0, object data, uint responseid)
        {
            if (_dataReceived != null)
            {
                Delegate[] delegates = _dataReceived.GetInvocationList();
                for (int i = 0; i < delegates.Length; ++i)
                {
                    ((ClientDataReceivedHandler<T, TServerClient>)delegates[i]).BeginInvoke(
                        server, arg0, data, responseid, EndRaiseEventAsync, null);
                }
            }
        }

        private void EndRaiseEventAsync(IAsyncResult iar)
        {
            ClientDataReceivedHandler<T, TServerClient> caller =
                (ClientDataReceivedHandler<T, TServerClient>)((AsyncResult)iar).AsyncDelegate;

            if (!caller.EndInvoke(iar))
            {
                Remove(caller);
            }
        }

        #endregion
    }

    internal sealed class ClientEventEntry
    {
        #region Variables

        private event DataReceivedHandler _dataReceived;

        internal readonly DeserializeData _deserialize;

        #endregion

        #region Constructors

        public ClientEventEntry(DeserializeData deserialize)
        {
            _deserialize = deserialize;
        }

        #endregion

        #region Methods

        public void Add(DataReceivedHandler callback)
        {
            _dataReceived += callback;
        }

        public void Remove(DataReceivedHandler callback)
        {
            _dataReceived -= callback;
        }

        public void RaiseAsync(IClient client, object result)
        {
            if (_dataReceived != null)
            {
                Delegate[] delegates = _dataReceived.GetInvocationList();
                for (int i = 0; i < delegates.Length; ++i)
                {
                    ((DataReceivedHandler)delegates[i]).BeginInvoke(client, result, EndRaiseEventAsync, null);
                }
            }
        }

        private void EndRaiseEventAsync(IAsyncResult iar)
        {
            DataReceivedHandler caller = (DataReceivedHandler)((AsyncResult)iar).AsyncDelegate;

            if (!caller.EndInvoke(iar))
            {
                Remove(caller);
            }
        }

        #endregion
    }
}