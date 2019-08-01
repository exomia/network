﻿#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

#define TCP

using System;
using System.Text;
using System.Threading.Tasks;
using Exomia.Network;
using Exomia.Network.DefaultPackets;
using Exomia.Network.TCP;

namespace Example.Client
{
    class Program
    {
        private static async Task Main(string[] args)
        {
#if TCP
            using (TcpClientEap client = new TcpClientEap())
#else
            using (var client = new UdpClientEap())
#endif
            {
                client.Disconnected += (c, r) => { Console.WriteLine(r + " | Disconnected"); };
                client.AddCommand(
                    (in Packet packet) =>
                    {
                        return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
                    }, 45);

                client.AddDataReceivedCallback(
                    45, (client1, data) =>
                    {
                        Console.WriteLine(data + " -- OK");
                        return true;
                    });

                Console.WriteLine(client.Connect("127.0.0.1", 3000) ? "CONNECTED" : "CONNECT FAILED");
                
                byte[] request = Encoding.UTF8.GetBytes("get time");
                for (int i = 0; i < 10; i++)
                {
                    Response<PingPacket> res = await client.SendRPing();
                    if (res)
                    {
                        Console.WriteLine(
                            i +
                            "ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.Timestamp) / 2)
                                                       .TotalMilliseconds);
                    }
                    else { Console.WriteLine("error receiving response"); }
                    
                    Response<string> res2 = await client.SendR(
                        45, request, 0, request.Length, (in Packet packet) =>
                        {
                            return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
                        });

                    Console.WriteLine(res2 ? res2.Result : "error receiving response");
                }

                Console.WriteLine("press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}