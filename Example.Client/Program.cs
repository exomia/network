#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

#define TCP

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private static async Task Main(string[] args)
        {
#if TCP
            TcpClientEap client = new TcpClientEap(8096);
#else
            var client = new UdpClientEap(8096);
#endif
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

            byte[] request = Encoding.UTF8.GetBytes(string.Join(" ", Enumerable.Range(1, 10_000)));
            Console.WriteLine(request.Length);
            for (int i = 0; i < 10; i++)
            {
                //Response<PingPacket> res = await client.SendRPing();
                //if (res)
                //{
                //    Console.WriteLine(
                //        i +
                //        "ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.Timestamp) / 2)
                //                                   .TotalMilliseconds);
                //}
                //else { Console.WriteLine("error receiving response"); }
                Stopwatch sw = Stopwatch.StartNew();
                Response<string> res2 = await client.SendR(
                    45, request, 0, request.Length, (in Packet packet) =>
                    {
                        return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
                    });

                sw.Stop();
                Console.WriteLine(
                    (res2 ? res2.Result : "error receiving response") + " - " + sw.ElapsedMilliseconds + "ms");
                Console.ReadKey();
            }

            Console.WriteLine("press any key to exit...");
            Console.ReadKey();

            client.Dispose();
        }
    }
}