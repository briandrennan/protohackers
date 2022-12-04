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
        var pipe = new Pipe();
        var readTask = ReadData(client, pipe.Writer, cts.Token);
        var sendTask = SendData(client, pipe.Reader, cts.Token);
        await Task.WhenAll(readTask, sendTask).ConfigureAwait(false);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutting down.");
}

async Task SendData(Socket client, PipeReader reader, CancellationToken token)
{
    try
    {
        var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
        if (readResult.IsCanceled)
        {
            reader.Complete();
            return;
        }

        var buffer = readResult.Buffer;
        var position = buffer.Start;
        var total = 0;
        while (true)
        {
            if (buffer.TryGet(ref position, out var memory, advance: true))
            {
                var transferred = await client.SendAsync(memory, token).ConfigureAwait(false);
                total += transferred;
                Console.WriteLine("- {0}: sent {1} bytes ({2} total)", client.RemoteEndPoint, transferred, total);
            }
            else
            {
                break;
            }
        }

        reader.Complete();
        await client.DisconnectAsync(true, token).ConfigureAwait(false);
        Console.WriteLine("- {0}: (disconnected)", client.RemoteEndPoint);
    }
    catch (Exception ex)
    {
        reader.Complete(ex);
    }
}

async Task ReadData(Socket client, PipeWriter writer, CancellationToken token)
{
    var totalLength = 0;
    var address = client.RemoteEndPoint;

    try
    {
        while (true)
        {
            var memory = writer.GetMemory();
            var bytesTransferred = await client.ReceiveAsync(memory, token).ConfigureAwait(false);
            totalLength += bytesTransferred;

            Console.WriteLine("- {0}: received chunk of length {1}", address, bytesTransferred);
            writer.Advance(bytesTransferred);

            // This indicates that the client transferred less memory than we had capacity to read,
            // ergo, the client is done transmitting to us.
            if (bytesTransferred < memory.Length)
            {
                break;
            }
        }
        writer.Complete();
    }
    catch (Exception ex)
    {
        writer.Complete(ex);
    }
}

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cts.Cancel();
}
