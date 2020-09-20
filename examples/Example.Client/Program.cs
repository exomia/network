#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

#define UDP

#if TCP
using Exomia.Network.TCP;
#else
using Exomia.Network.UDP;
#endif
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exomia.Network;

namespace Example.Client
{
    class Program
    {
        private static async Task Main(string[] args)
        {
#if TCP
            using TcpClientEap client = new TcpClientEap(512);
#else
            using UdpClientEap client = new UdpClientEap(512);
#endif
            client.Disconnected += (c, r) => { Console.WriteLine(r + " | Disconnected"); };
            client.AddCommand(
                (in Packet packet) =>
                {
                    Console.WriteLine(
                        "{0} >= {1}  ==  {2}", packet.Buffer.Length, packet.Length,
                        packet.Buffer.Length >= packet.Length);
                    return true;
                }, 1);

            client.AddCommand(
                (in Packet packet) =>
                {
                    return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
                }, 45);

            client.AddDataReceivedCallback(
                45, (client1, data, responseID) =>
                {
                    Console.WriteLine(data + " -- OK");
                    return true;
                });
            Thread.Sleep(100);
            Console.WriteLine(
                client.Connect("127.0.0.1", 3000, b => b.ReceiveBufferSize = 64 * 1024)
                    ? "CONNECTED"
                    : "CONNECT FAILED");

            for (int n = 1; n <= 8; n++)
            {
                byte[] b = new byte[4096 * n];
                client.Send(1, b, 0, b.Length);
            }
            byte[] request = Encoding.UTF8.GetBytes(string.Join(" ", Enumerable.Range(1, 1_000)));
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
                    i + ": " + (res2 ? res2.Result : "error receiving response") + " - " +
                    (sw.ElapsedMilliseconds / 2) + "ms");
            }

            Console.WriteLine("press any key to exit...");
            Console.ReadKey();
        }
    }
}