#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

#define TCP

using System;
using System.Text;
using Exomia.Network;
using Exomia.Network.TCP;

namespace Example.Server
{
    class Program
    {
        private static void Main(string[] args)
        {
            using (Server server = new Server())
            {
                server.ClientConnected += (server1, client) =>
                {
                    Console.WriteLine("Client connected: " + client.IPAddress);
                };
                server.ClientDisconnected += (server1, client, reason) =>
                {
                    Console.WriteLine(reason + " Client disconnected: " + client.IPAddress);
                };
                server.AddCommand(
                    (in Packet packet) =>
                    {
                        return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
                    }, 45);
                server.AddDataReceivedCallback(
                    45, (server1, client, data, responseid) =>
                    {
                        string request = (string)data;
                        Console.WriteLine($"Request: {request}");
                        byte[] buffer = Encoding.UTF8.GetBytes(DateTime.Now.ToLongDateString());
                        server1.SendTo(client, 45, buffer, 0, buffer.Length, responseid);
                        return true;
                    });

                Console.WriteLine(server.Run(3000));

                Console.WriteLine("press any key to exit...");
                Console.ReadKey();
            }
        }
    }

#if TCP
    class Server : TcpServerEapBase<ServerClient>
#else
    class Server : UdpServerEapBase<ServerClient>
#endif
    {
        protected override bool CreateServerClient(out ServerClient serverClient)
        {
            serverClient = new ServerClient();
            return true;
        }

        public Server(ushort expectedMaxClient = 32, ushort maxPacketSize = 65520)
            : base(expectedMaxClient, maxPacketSize) { }
    }

#if TCP
    class ServerClient : TcpServerClientBase
#else
    class ServerClient : UdpServerClientBase
#endif
    { }
}