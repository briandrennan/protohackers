// See https://aka.ms/new-console-template for more information
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("0. Smoke Test");


using var cts = new CancellationTokenSource();
Console.CancelKeyPress += Console_CancelKeyPress;

var serverEndpoint = new IPEndPoint(IPAddress.Any, 7);
using var server = new Socket(serverEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

server.Bind(serverEndpoint);
server.Listen();
Console.WriteLine("Listening on endpoint {0}", serverEndpoint);

try
{
    while (!cts.IsCancellationRequested)
    {
        var client = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
        Console.WriteLine("Connection received from client {0}", client.RemoteEndPoint);
        await HandleEchoAsync(client, cts).ConfigureAwait(false);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutting down.");
}

async Task HandleEchoAsync(Socket client, CancellationTokenSource cts)
{
    var buffer = ArrayPool<byte>.Shared.Rent(1024);
    try
    {
        var memory = buffer.AsMemory();
        var bytesTransfered = await client.ReceiveAsync(memory, cts.Token).ConfigureAwait(false);

        var send = memory[..bytesTransfered];
        var clientMessage = Encoding.ASCII.GetString(send.ToArray());
        Console.WriteLine("- {0}: received {1}", client.RemoteEndPoint, clientMessage);
        await client.SendAsync(send, cts.Token).ConfigureAwait(false);
        Console.WriteLine("- {0}: sent {1}", client.RemoteEndPoint, clientMessage);
        var address = client.RemoteEndPoint;
        await client.DisconnectAsync(true, cts.Token).ConfigureAwait(false);
        Console.WriteLine("- {0}: (disconnected)", address);
    }
    finally
    {
        if (!cts.IsCancellationRequested)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cts.Cancel();
}
