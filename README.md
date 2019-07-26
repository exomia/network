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
static void Main(string[] args)
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
					"ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.TimeStamp) / 2)
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
    protected override bool CreateServerClient(EndPoint arg0, out UdpServerClient serverClient)
    {
        serverClient = new UdpServerClient(arg0);
        return true;
    }

    /// <inheritdoc />
    public UdpServer(uint maxClients, int maxPacketSize = 65522)
        : base(maxClients, maxPacketSize) { }
}

class UdpServerClient : ServerClientBase<EndPoint>
{
    public UdpServerClient(EndPoint arg0) : base(arg0) { }
    public override IPAddress IPAddress { get { return (_arg0 as IPEndPoint)?.Address; } }
    public override EndPoint EndPoint { get { return _arg0; } }
}
```

```csharp
static void Main(string[] args)
{
	using(UdpServer server = new UdpServer(32))
	{
		server.ClientConnected += (endpoint) =>
		{
		    Console.WriteLine("Client connected: " + (endpoint as IPEndPoint));
		};
		server.ClientDisconnected += (endpoint, reason) =>
		{
		    Console.WriteLine(reason + " Client disconnected: " + (endpoint as IPEndPoint));
		};
		server.Run(3001);

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

-Client-TCP

```csharp
static void Main(string[] args)
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
					"ping received " + TimeSpan.FromTicks((DateTime.Now.Ticks - res.Result.TimeStamp) / 2)
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
    protected override bool CreateServerClient(Socket arg0, out TcpServerClient serverClient)
    {
        serverClient = new TcpServerClient(arg0);
        return true;
    }

    /// <inheritdoc />
    public TcpServer(int maxPacketSize = 65520)
        : base(maxPacketSize) { }
}

class TcpServerClient : ServerClientBase<Socket>
{
    public TcpServerClient(Socket arg0) : base(arg0) { }
    public override IPAddress IPAddress { get { return (_arg0.RemoteEndPoint as IPEndPoint)?.Address; } }
    public override EndPoint EndPoint { get { return _arg0.RemoteEndPoint; } }
}
```

```csharp
static void Main(string[] args)
{
	using(TcpServer server = new TcpServer())
	{
		server.ClientConnected += (socket) =>
		{
		    Console.WriteLine("Client connected: " + (socket.RemoteEndPoint as IPEndPoint));
		};
		server.ClientDisconnected += (socket, reason) =>
		{
		    Console.WriteLine(reason + " Client disconnected: " + (socket.RemoteEndPoint as IPEndPoint));
		};

		server.AddCommand(45, (in Packet packet) =>
        {
            return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
        });

		server.AddDataReceivedCallback(45, (b, arg0, data, responseid) =>
        {
			string request = (string)data;
			Console.WriteLine($"Request: {request}");
			byte[] buffer = Encoding.UTF8.GetBytes(DateTime.Now.ToLongDateString());
			b.SendTo(arg0, 45, buffer, 0, buffer.Length, responseid);
			return true;
        });
        
		server.Run(3000);

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

