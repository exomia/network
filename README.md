## Information

exomia/network is a wrapper library around System.Socket for easy TCP/UDP client & server communication.

![](https://img.shields.io/github/issues-pr/exomia/network.svg) ![](https://img.shields.io/github/issues/exomia/network.svg)  ![](https://img.shields.io/github/last-commit/exomia/network.svg) ![](https://img.shields.io/github/contributors/exomia/network.svg) ![](https://img.shields.io/github/commit-activity/y/exomia/network.svg) ![](https://img.shields.io/github/languages/top/exomia/network.svg) ![](https://img.shields.io/github/languages/count/exomia/network.svg) ![](https://img.shields.io/github/license/exomia/network.svg)

## Example

-Client-UDP

```csharp
using (ClientEap client = new ClientEap())
{
    client.Disconnected += (c) => { Console.WriteLine("Disconnected"); };
    if (client.Connect(SocketMode.Udp, "127.0.0.1", 3001)) { Console.WriteLine("CONNECTED"); }
    else { Console.WriteLine("CONNECTION FAILED"); }

    for (int i = 0; i < 10; i++)
    {
        Response<PING_STRUCT> res = await client.SendRPing();
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
    public UdpServer(uint maxClients, int maxPacketSize = 65523)
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
		server.ClientDisconnected += (endpoint) =>
		{
		    Console.WriteLine("Client disconnected: " + (endpoint as IPEndPoint));
		};
		server.Run(3001);

		Console.WriteLine("press any key to exit...");
		Console.ReadKey();
	}
}
```

-Client-TCP

```csharp
using (ClientEap client = new ClientEap())
{
	client.Disconnected += (c) => { Console.WriteLine("Disconnected"); };
	client.AddCommand(45, (in Packet packet) =>
	{
		return Encoding.UTF8.GetString(packet.Buffer, packet.Offset, packet.Length);
	});

	client.AddDataReceivedCallback(45, (client1, data) =>
	{
		Console.WriteLine(data + " -- OK");
		return true;
	});

	if (client.Connect(SocketMode.TCP, "127.0.0.1", 3000)) { Console.WriteLine("CONNECTED"); }
	else { Console.WriteLine("CONNECTION FAILED"); }
	byte[] request = Encoding.UTF8.GetBytes("get time");
	for (int i = 0; i < 10; i++)
	{
		Response<PING_STRUCT> res = await client.SendRPing();
		if (res.Success)
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
    public TcpServer(int maxPacketSize = 0)
        : base(maxPacketSize) { }
}

class TcpServerClient : ServerClientBase<Socket>
{
    public UdpServerClient(Socketarg0) : base(arg0) { }
    public override IPAddress IPAddress { get { return (_arg0.RemoteEndPoint as IPEndPoint)?.Address; } }
    public override EndPoint EndPoint { get { return _arg0.RemoteEndPoint; } }
}
```

```csharp
static void Main(string[] args)
{
	using(TcpServer server = new TcpServer())
	{
		server.ClientConnected += (endpoint) =>
		{
		    Console.WriteLine("Client connected: " + (endpoint as IPEndPoint));
		};
		server.ClientDisconnected += (endpoint) =>
		{
		    Console.WriteLine("Client disconnected: " + (endpoint as IPEndPoint));
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

## Installing

```shell
[Package Manager]
PM> Install-Package Exomia.Network
```

## Send data

to send data to a server or from a server to a client you have several options see:

 - IClient.cs
 
```csharp
/// <summary>
///     send data to the server
/// </summary>
/// <param name="commandid">command id</param>
/// <param name="data">data</param>
/// <param name="offset">offset</param>
/// <param name="lenght">lenght of data</param>
void Send(uint commandid, byte[] data, int offset, int lenght);

/// <summary>
///     send data to the server
/// </summary>
/// <param name="commandid">command id</param>
/// <param name="serializable">ISerializable</param>
void Send(uint commandid, ISerializable serializable);

/// <summary>
///     send data to the server
/// </summary>
/// <typeparam name="T">struct type</typeparam>
/// <param name="commandid">command id</param>
/// <param name="data">struct data</param>
void Send<T>(uint commandid, in T data) where T : struct;

...
```
alternative you can use the 'SendR' methods to wait until you received the response

Samples:
```csharp
 /// <summary>
 ///     send data to the server
 /// </summary>
 /// <typeparam name="TResult">struct type</typeparam>
 /// <param name="commandid">command id</param>
 /// <param name="data">data</param>
 /// <param name="offset">offset</param>
 /// <param name="lenght">lenght of data</param>
 /// <param name="timeout">timeout</param>
 /// <returns></returns>
Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght, TimeSpan timeout)
    where TResult : struct;

/// <summary>
///     send data to the server
/// </summary>
/// <typeparam name="TResult">struct type</typeparam>
/// <param name="commandid">command id</param>
/// <param name="data">data</param>
/// <param name="offset">offset</param>
/// <param name="lenght">lenght of data</param>
/// <param name="deserialize"></param>
/// <param name="timeout">timeout</param>
/// <returns></returns>
Task<Response<TResult>> SendR<TResult>(uint commandid, byte[] data, int offset, int lenght,
    DeserializePacket<TResult> deserialize, TimeSpan timeout);
    
...
```

- IServer.cs

```csharp
/// <summary>
///     send data to the client
/// </summary>
/// <param name="arg0">Socket|EndPoint</param>
/// <param name="commandid">command id</param>
/// <param name="data">data</param>
/// <param name="offset">offset</param>
/// <param name="lenght">data lenght</param>
/// <param name="responseid">responseid</param>
void SendTo(T arg0, uint commandid, byte[] data, int offset, int lenght, uint responseid);

/// <summary>
///     send data to the client
/// </summary>
/// <param name="arg0">Socket|EndPoint</param>
/// <param name="commandid">command id</param>
/// <param name="serializable">ISerializable</param>
/// <param name="responseid">responseid</param>
void SendTo(T arg0, uint commandid, ISerializable serializable, uint responseid);

/// <summary>
///     send data to the client
/// </summary>
/// <typeparam name="T1">struct type</typeparam>
/// <param name="arg0">Socket|EndPoint</param>
/// <param name="commandid">command id</param>
/// <param name="data">data</param>
/// <param name="responseid">responseid</param>
void SendTo<T1>(T arg0, uint commandid, in T1 data, uint responseid) where T1 : struct;

...
```

## Changelog

### v1.1.1.1

	- added eap and apm versions of client aswell tcp/udp-server
	- bug fixes (disconnect reason, ...)
	- server only accepts requests from connected clients
	- better abstraction and cleaner code
	- impl. SendError to handle send failures
	- ...

## License

MIT License
Copyright (c) 2018 exomia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


