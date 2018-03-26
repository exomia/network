using System;
using System.Runtime.Remoting.Messaging;

namespace Exomia.Network.Lib
{
    internal sealed class ServerClientEventEntry<T, TServerClient>
        where T : class
        where TServerClient : ServerClientBase<T>
    {
        private event ClientDataReceivedHandler<T, TServerClient> _dataReceived;

        public void Add(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived += callback;
        }

        public void Remove(ClientDataReceivedHandler<T, TServerClient> callback)
        {
            _dataReceived -= callback;
        }

        public void RaiseAsync(ServerBase<T, TServerClient> server, T arg0, object data)
        {
            if (_dataReceived != null)
            {
                Delegate[] delegates = _dataReceived.GetInvocationList();
                for (int i = 0; i < delegates.Length; i++)
                {
                    ((ClientDataReceivedHandler<T, TServerClient>)delegates[i]).BeginInvoke(
                        server, arg0, data, EndRaiseEventAsync, null);
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
    }

    internal sealed class ClientEventEntry
    {
        private event DataReceivedHandler _dataReceived;

        public void Add(DataReceivedHandler callback)
        {
            _dataReceived += callback;
        }

        public void Remove(DataReceivedHandler callback)
        {
            _dataReceived -= callback;
        }

        public void RaiseAsync(IClient client, object data)
        {
            if (_dataReceived != null)
            {
                Delegate[] delegates = _dataReceived.GetInvocationList();
                for (int i = 0; i < delegates.Length; i++)
                {
                    ((DataReceivedHandler)delegates[i]).BeginInvoke(client, data, EndRaiseEventAsync, null);
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
    }
}