// See https://aka.ms/new-console-template for more information
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

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
    var pipe = new Pipe();
    var writer = pipe.Writer;
    var reader = pipe.Reader;
    var bytesTransferred = 0;
    var address = client.RemoteEndPoint;

    try
    {
        var totalLength = 0;

        while (true)
        {
            var memory = writer.GetMemory(256);
            bytesTransferred = await client.ReceiveAsync(memory, cts.Token).ConfigureAwait(false);
            totalLength += bytesTransferred;

            Console.WriteLine("- {0}: received chunk of length {1}", address, bytesTransferred);
            writer.Advance(bytesTransferred);

            if (bytesTransferred < memory.Length)
            {
                break;
            }
        }
        writer.Complete();

        var readResult = await reader.ReadAsync(cts.Token).ConfigureAwait(false);
        if (readResult.IsCanceled)
        {
            return;
        }

        var buffer = readResult.Buffer;
        SequencePosition consumed = buffer.Start;
        var totalSent = 0;
        while (true)
        {
            if (buffer.TryGet(ref consumed, out var memory, advance: true))
            {
                await client.SendAsync(memory, cts.Token).ConfigureAwait(false);
                totalSent += memory.Length;
                Console.WriteLine("- {0}: sent chunk {1} ({2}/{3})", address, memory.Length, totalSent, totalLength);
            }
            else
            {
                break;
            }
        }

        await client.DisconnectAsync(true, cts.Token).ConfigureAwait(false);
        Console.WriteLine("- {0}: (disconnected)", address);
    }
    catch (Exception ex)
    {
        writer.Complete(ex);
        reader.Complete(ex);
    }
    finally
    {
        reader.Complete();
    }
}

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cts.Cancel();
}
