## Information

exomia/network is a wrapper library around System.Socket for easy TCP/UDP client & server communication.

![](https://img.shields.io/github/issues-pr/exomia/network.svg)
![](https://img.shields.io/github/issues/exomia/network.svg)
![](https://img.shields.io/github/last-commit/exomia/network.svg)
![](https://img.shields.io/github/contributors/exomia/network.svg)
![](https://img.shields.io/github/commit-activity/y/exomia/network.svg)
![](https://img.shields.io/github/languages/top/exomia/network.svg)
![](https://img.shields.io/github/languages/count/exomia/network.svg)
![](https://img.shields.io/github/license/exomia/network.svg)

## Installing

```shell
[Package Manager]
PM> Install-Package Exomia.Network
```

## Example

-Client-UDP

```csharp
static async Task Main(string[] args)
{
	using(UdpClientEap client = new UdpClientEap())
	{
		client.Disconnected += (c, r) => { Console.WriteLine(r + " | Disconnected"); };
		
		Console.WriteLine(client.Connect("127.0.0.1", 3000) ? "CONNECTED" : "CONNECT FAILED");

		for (int i = 0; i < 10; i++)
		{
			Response<PingPacket> res = await client.SendRPing();
			if (res)
			{
				Console.WriteLine(i +
					"ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.Timestamp) / 2)
						.TotalMilliseconds);
			}
			else { Console.WriteLine("error receiving response"); }
		}

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

-Server-UDP

```csharp
class UdpServer : UdpServerEapBase<UdpServerClient>
{
    protected override bool CreateServerClient(out UdpServerClient serverClient)
    {
        serverClient = new UdpServerClient();
        return true;
    }

    /// <inheritdoc />
    public UdpServer(ushort maxClients, ushort maxPacketSize = 65522)
        : base(maxClients, maxPacketSize) { }
}

class UdpServerClient : UdpServerClientBase
{
    public UdpServerClient() { }
}
```

```csharp
static void Main(string[] args)
{
	using(UdpServer server = new UdpServer(32))
	{
		server.ClientConnected += (server1, client) =>
		{
		    Console.WriteLine("Client connected: " + (client.IPAddress));
		};
		server.ClientDisconnected += (server1, client, reason) =>
		{
		    Console.WriteLine(reason + " Client disconnected: " + (client.IPAddress));
		};
		server.Run(3001);

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

-Client-TCP

```csharp
static async Task Main(string[] args)
{
	using (TcpClientEap client = new TcpClientEap())
	{
		client.Disconnected += (c, r) => { Console.WriteLine(r + " | Disconnected"); };
		client.AddCommand(45, (in Packet packet) =>
		{
			return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
		});

		client.AddDataReceivedCallback(45, (client1, data) =>
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
				Console.WriteLine(i +
					"ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.Timestamp) / 2)
						.TotalMilliseconds);
			}
			else { Console.WriteLine("error receiving response"); }

			Response<string> res2 = await client.SendR<string>(45, request, 0, request .Length, (in Packet packet) =>
			{
				return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
			});

			if (res2)
			{
				Console.WriteLine(res2.Result);
			}
			else { Console.WriteLine("error receiving response"); }
		}

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

-Server-TCP

```csharp
class TcpServer : TcpServerEapBase<TcpServerClient>
{
    protected override bool CreateServerClient(out TcpServerClient serverClient)
    {
        serverClient = new TcpServerClient();
        return true;
    }

    public TcpServer(ushort expectedMaxClient = 32, ushort maxPacketSize = 65520)
        : base(expectedMaxClient, maxPacketSize) { }
}

class TcpServerClient : TcpServerClientBase
{
    public TcpServerClient() { }
}
```

```csharp
static void Main(string[] args)
{
	using(TcpServer server = new TcpServer())
	{
		server.ClientConnected += (server1, client) =>
		{
		    Console.WriteLine("Client connected: " + (client.IPAddress));
		};
		server.ClientDisconnected += (server1, client, reason) =>
		{
		    Console.WriteLine(reason + " Client disconnected: " + (client.IPAddress));
		};

		server.AddCommand(45, (in Packet packet) =>
        {
            return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
        });

		server.AddDataReceivedCallback(45, (server1, client, data, responseid) =>
        {
			string request = (string)data;
			Console.WriteLine($"Request: {request}");
			byte[] buffer = Encoding.UTF8.GetBytes(DateTime.Now.ToLongDateString());
			server1.SendTo(client, 45, buffer, 0, buffer.Length, responseid);
			return true;
        });
        
		server.Run(3000);

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

