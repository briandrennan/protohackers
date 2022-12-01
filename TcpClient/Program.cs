using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("TCP Client!");

if (args.Length != 2)
{
    Console.WriteLine("Usage: [address] [port]");
    return -1;
}

var address = IPAddress.Parse(args[0]);
var port = int.Parse(args[1]);

var endpoint = new IPEndPoint(address, port);
var clientPort = (ushort)Random.Shared.Next(IPEndPoint.MaxPort - 10000, IPEndPoint.MaxPort - 1);
Console.WriteLine("Connecting to server {0} from port {1}", endpoint, clientPort);
var client = new IPEndPoint(IPAddress.Loopback, clientPort);
var server = new Socket(client.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
await server.ConnectAsync(endpoint).ConfigureAwait(false);

string message;
if (!Console.IsInputRedirected)
{
    Console.Write("Message: ");
    message = Console.ReadLine()!;
}
else
{
    message = Console.In.ReadToEnd();
    Console.WriteLine("Send: {0}", message);
}

var bytes = Encoding.ASCII.GetBytes(message!);

await server.SendAsync(bytes).ConfigureAwait(false);
var buffer = new ArraySegment<byte>(new byte[4096]);
var response = server.ReceiveAsync(buffer).ConfigureAwait(false);
message = Encoding.ASCII.GetString(buffer);
Console.WriteLine("Response: {0}", message);

return 0;
