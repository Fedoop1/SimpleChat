using System.IO.Pipes;
using System.Text;

const string serverPipeName = "SimpleChat.Server";
const string userNameMetadataKey = "userName";
const int defaultMessageSize = 256;
const int readDelay = 500;

CancellationTokenSource cts = new();
Dictionary<string, PipeStream> connectedUsers = new();
Dictionary<string, List<string>> messagesStorage = new();

try
{
    while (!cts.IsCancellationRequested)
    {
        var chatServerStream = GetNewPipeStream();

        await WaitForClientAsync(chatServerStream, cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Application was stopped gracefully");
}

async Task WaitForClientAsync(NamedPipeServerStream stream, CancellationToken token)
{
    token.ThrowIfCancellationRequested();

    Console.WriteLine($"Listening for a client in thread #{Thread.CurrentThread.ManagedThreadId}");

    await stream.WaitForConnectionAsync(token);
    HandleClientConnectionAsync(stream, token);
}

Task HandleClientConnectionAsync(NamedPipeServerStream stream, CancellationToken token) => Task.Run(async () =>
{
    Console.WriteLine("Connection attempt");
    var userName = await ReadUserMetadataAsync(stream, token);

    if (string.IsNullOrEmpty(userName))
    {
        await CloseConnectionAsync(stream);
        Console.WriteLine("Invalid connection attempt. Connection was closed");
        return;
    }

    if (!connectedUsers.TryAdd(userName, stream)) connectedUsers[userName] = stream;

    Console.WriteLine($"User {userName} connected to the chat");

    var writingThread = Task.CompletedTask;

    if (messagesStorage.TryGetValue(userName, out var messages))
    {
        writingThread = Task.Run(() => SendMessageHistoryToClientAsync(stream, userName, messages.Count, token), token);
    }

    var readingThread = Task.Run(() => ReadMessagesFromClientAsync(stream, userName, token), token);

    await Task.WhenAll(readingThread, writingThread);
    await CloseConnectionAsync(stream);

    connectedUsers.Remove(userName);

    Console.WriteLine($"User {userName} disconnected from the chat");
}, token);

async Task ReadMessagesFromClientAsync(PipeStream stream, string clientName, CancellationToken token)
{
    int messageNumber = default;

    while (stream.IsConnected && !token.IsCancellationRequested)
    {
        token.ThrowIfCancellationRequested();

        var message = await ReadMessageFromStreamAsync(stream, token);

        if (string.IsNullOrEmpty(message))
        {
            await Task.Delay(readDelay);
            continue;
        }

        if (!messagesStorage.TryAdd(clientName, new() { message }))
        {
            messagesStorage[clientName].Add(message);
        }

        Console.WriteLine($"Message #{++messageNumber} from {clientName} was received...");
        Console.WriteLine($"Message content:\n{message}");

        await BroadcastMessagesAsync(clientName, message);
    }
}

async Task<string?> ReadMessageFromStreamAsync(Stream stream, CancellationToken token)
{
    var buffer = new byte[defaultMessageSize];

    await stream.ReadAsync(buffer, token);

    return Encoding.Default.GetString(buffer).TrimEnd('\0');
}

async Task SendMessageHistoryToClientAsync(Stream stream, string clientName, int numberOfMessages, CancellationToken token)
{
    int messageNumber = default;
    foreach (var message in messagesStorage[clientName].GetRange(0, numberOfMessages))
    {
        token.ThrowIfCancellationRequested();

        var messageBytes = Encoding.Default.GetBytes(message);

        Console.WriteLine($"Sending #{++messageNumber} {clientName}'s message from the history");
        await stream.WriteAsync(messageBytes);
        Console.WriteLine($"{clientName}'s #{messageNumber} was delivered");
    }
}

Task BroadcastMessagesAsync(string clientName, string message)
{
    if (connectedUsers.Count <= 1) return Task.CompletedTask;

    var buffer = new byte[defaultMessageSize];
    Encoding.Default.GetBytes(message, buffer);
    Console.WriteLine($"Broadcasting {clientName}'s message");

    var broadcastTasks = connectedUsers
        .Where(kvp => kvp.Key != clientName && kvp.Value.IsConnected).Select(async kvp =>
        {
            Console.WriteLine($"Broadcasting message to {kvp.Key}...");
            await kvp.Value.WriteAsync(buffer);

            Console.WriteLine($"Broadcast message was delivered to {kvp.Key}");
        });

    return Task.WhenAll(broadcastTasks);
}

async Task<string?> ReadUserMetadataAsync(PipeStream stream, CancellationToken token)
{
    var connectionAttempts = 10;
    string? clientMetadata = default;

    while (stream.IsConnected && connectionAttempts-- > 0)
    {
        clientMetadata = await ReadMessageFromStreamAsync(stream, token);

        if (!string.IsNullOrEmpty(clientMetadata)) break;

        await Task.Delay(readDelay);
    }

    if (connectionAttempts == 0 || string.IsNullOrEmpty(clientMetadata) || !clientMetadata.StartsWith(userNameMetadataKey)) return null;

    return clientMetadata[(userNameMetadataKey.Length + 1)..];
}

static NamedPipeServerStream GetNewPipeStream() => new(serverPipeName, PipeDirection.InOut, Environment.ProcessorCount, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

static async Task CloseConnectionAsync(NamedPipeServerStream stream)
{
    stream.Disconnect();
    await stream.DisposeAsync();
}