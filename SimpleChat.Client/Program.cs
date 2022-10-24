using System.IO.Pipes;
using System.Text;
using SimpleChat.Client;

const int readDelay = 500;
const int defaultMessageSize = 256;
const string serverPipeName = "SimpleChat.Server";
const string userNameMetadataKey = "userName";
const string localComputerName = ".";

var cts = new CancellationTokenSource();

Console.WriteLine("Welcome to simple chat, please enter your name: ");
var name = Console.ReadLine();

Console.WriteLine("Please enter message count: ");
var messageCount = int.Parse(Console.ReadLine() ?? "10");

Console.WriteLine("Please enter message length: ");
var messageLength = int.Parse(Console.ReadLine() ?? "5" );

Console.WriteLine("Press ESC to cancel");
var cancellationTask = ReadForCancellation(cts);

var chatServerStream =
    new NamedPipeClientStream(localComputerName, serverPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

try
{
    Console.WriteLine("Connecting to the server...");
    await chatServerStream.ConnectAsync(cts.Token);

    Console.WriteLine($"Result: {(chatServerStream.IsConnected ? "connected" : "disconnected")}");

    if (!chatServerStream.IsConnected) return;

    await SendMetadataMessageAsync();

    var writerTask = Task.Run(() => WriteMessages(chatServerStream, messageCount, messageLength, cts.Token), cts.Token);
    var readerTask = Task.Run(() => ReadMessages(chatServerStream, cts.Token), cts.Token);

    Task.WaitAny(cancellationTask, readerTask);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled gracefully");
}
catch (Exception e)
{
    Console.BackgroundColor = ConsoleColor.DarkRed;
    Console.ForegroundColor = ConsoleColor.White;

    Console.WriteLine("Unexpected exception has occurred...");
    Console.WriteLine($"Exception message: {e.Message}");

    Console.ResetColor();
}
finally
{
    await CloseServerConnection();
}

async Task SendMetadataMessageAsync()
{
    Console.WriteLine("Sending metadata message...");
    var metaMessage = Encoding.Default.GetBytes($"{userNameMetadataKey}:{name}");
    await chatServerStream!.WriteAsync(metaMessage);
    Console.WriteLine("Metadata message was delivered");
}

async Task CloseServerConnection()
{
    Console.WriteLine("Disconnecting from the server...");
    await chatServerStream!.DisposeAsync();
    Console.WriteLine("Disconnected");
}


static async Task WriteMessages(PipeStream stream, int messageCount, int messageLength, CancellationToken token)
{
    int messageNumber = default;
    var buffer = new byte[defaultMessageSize];

    foreach (var generatedMessage in MessageSource.GetMessages(messageCount, messageLength))
    {
        token.ThrowIfCancellationRequested();
        if (!stream.IsConnected) return;

        Encoding.Default.GetBytes(generatedMessage, buffer);
        Console.WriteLine($"Sending #{++messageNumber} message to the server...");
        Console.WriteLine($"Message content:\n{generatedMessage}");

        await stream.WriteAsync(buffer, token);

        Console.WriteLine("Message was delivered");
    }
}

static async Task ReadMessages(PipeStream stream, CancellationToken token)
{
    int messageNumber = default;

    while (!token.IsCancellationRequested && stream.IsConnected)
    {
        token.ThrowIfCancellationRequested();

        var message = await ReadMessageFromStreamAsync(stream, token);

        if (string.IsNullOrEmpty(message))
        {
            await Task.Delay(readDelay);
            continue;
        }

        Console.WriteLine($"Message #{++messageNumber} was received...");
        Console.WriteLine($"Message content:\n{message}");
    }
}

static Task ReadForCancellation(CancellationTokenSource cts) => Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        var keyInfo = Console.ReadKey(true);

        if (keyInfo.Key != ConsoleKey.Escape) continue;

        Console.WriteLine("Execute cancellation...");
        cts.Cancel();
    }
});

static async Task<string?> ReadMessageFromStreamAsync(Stream stream, CancellationToken token)
{
    var buffer = new byte[defaultMessageSize];

    var readedByte = await stream.ReadAsync(buffer, token);

    return readedByte == default ? null : Encoding.Default.GetString(buffer).TrimEnd('\0');
}
