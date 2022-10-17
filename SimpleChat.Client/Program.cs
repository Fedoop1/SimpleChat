using System.IO.Pipes;
using System.Text;
using SimpleChat.Client;

const int ReadDelay = 500;
var cts = new CancellationTokenSource();
var random = new Random();

Console.WriteLine("Welcome to simple chat, please enter your name: ");
var name = Console.ReadLine();

Console.WriteLine("Please enter message count: ");
var messageCount = int.Parse(Console.ReadLine() ?? "10");

Console.WriteLine("Please enter message length: ");
var messageLength = int.Parse(Console.ReadLine() ?? "5" );

Console.WriteLine("Press ESC to cancel");
var cancellationTask = ReadForCancellation();

var chatServerStream = new NamedPipeClientStream("SimpleChat.Server", "MainPipe", PipeDirection.InOut, PipeOptions.Asynchronous);

try
{
    Console.WriteLine("Connecting to server...");
    await chatServerStream.ConnectAsync(cts.Token);

    Console.WriteLine($"Result: {(chatServerStream.IsConnected ? "connected" : "disconnected")}");

    if (!chatServerStream.IsConnected) return;

    await Task.Run(WriteMessages, cts.Token);
    var readerTask = Task.Run(ReadMessages, cts.Token);

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

async Task CloseServerConnection()
{
    Console.WriteLine("Disconnecting from the server...");
    await chatServerStream!.DisposeAsync();
    Console.WriteLine("Disconnected");
}


async Task WriteMessages()
{
    foreach (var generatedMessage in MessageSource.GetMessages(messageCount, messageLength))
    {
        cts.Token.ThrowIfCancellationRequested();
        if (!chatServerStream.IsConnected) return;

        var messageWithOverhead = AddMessageOverhead(generatedMessage);
        var byteMessage = Encoding.Default.GetBytes(messageWithOverhead);
        Console.WriteLine($"Sending \"{messageWithOverhead}\" to server...");
        await chatServerStream.WriteAsync(byteMessage, cts.Token);
        Console.WriteLine("Message was delivered");
        Thread.Sleep(random.Next(1000, 3000));
    }
}

async Task ReadMessages()
{
    var chatStreamReader = new StreamReader(chatServerStream, Encoding.Default);
    var serverMessage = string.Empty;

    while (!cts.IsCancellationRequested && chatServerStream.IsConnected)
    {
        cts.Token.ThrowIfCancellationRequested();

        if (chatStreamReader.EndOfStream)
        {
            Thread.Sleep(ReadDelay);
            continue;
        }

        serverMessage = await chatStreamReader.ReadLineAsync();

        Console.WriteLine("Message was received...");
        Console.WriteLine($"Message content:\n{serverMessage}");
    }
}

Task ReadForCancellation() => Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        var keyInfo = Console.ReadKey(true);

        if (keyInfo.Key != ConsoleKey.Escape) continue;

        Console.WriteLine("Execute cancellation...");
        cts.Cancel();
    }
});

string AddMessageOverhead(string message) => $"{DateTime.Now} | {name} | {message}";
