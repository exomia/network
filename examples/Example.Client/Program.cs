﻿#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

#define TCP

using System;
using System.Linq;
using System.Text;
using System.Threading;
using Exomia.Network;
#if TCP
using Exomia.Network.TCP;

#else
using Exomia.Network.UDP;
#endif

namespace Example.Client
{
    class Program
    {
        private static void Main(string[] args)
        {
#if TCP
            using TcpClientEap client = new TcpClientEap(512);
#else
            using UdpClientEap client = new UdpClientEap(512);
#endif
            client.Disconnected += (c, r) => { Console.WriteLine(r + " | Disconnected"); };

            client.AddCommand<string>(1, DeserializePacketToString);

            client.AddDataReceivedCallback(
                1, (IClient client1, in string request, ushort responseID) =>
                {
                    client1.Send(123, BitConverter.GetBytes(33));
                    SendRequestAndWaitForResponse(client1, request, responseID);
                    return true;
                });

            Thread.Sleep(100);
            Console.WriteLine(
                client.Connect("127.0.0.1", 3000, b => b.ReceiveBufferSize = 64 * 1024)
                    ? "CONNECTED"
                    : "CONNECT FAILED");

            Console.WriteLine("press any key to exit...");
            Console.ReadKey();
        }

        private static bool DeserializePacketToString(in Packet packet, out string s)
        {
            s = packet.ToString();
            return true;
        }

        private static async void SendRequestAndWaitForResponse(IClient client, string data, ushort responseID)
        {
            byte[] response =
                Encoding.UTF8.GetBytes(data + "World " + string.Join(", ", Enumerable.Range(1, 1_000_000)));
            Response<string> result = await client.SendR<string>(
                responseID, response, 0, response.Length, DeserializePacketToString, TimeSpan.FromSeconds(30), true);

            Console.WriteLine("GOT({1}): {0}", result.Result, result.SendError);
        }
    }
}