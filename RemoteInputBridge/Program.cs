using Grpc.Net.Client;
using GRPCRemote;
using RemoteInputBridge;

try
{
    var positional = new List<string>();
    var endpoint = Environment.GetEnvironmentVariable("GRPC_REMOTE_ENDPOINT") ?? "http://127.0.0.1:9036";
    var pipeName = "GRPCRemoteInput";
    for (var index = 0; index < args.Length; index++)
    {
        if (args[index] is "--endpoint" or "--pipe")
        {
            var option = args[index];
            if (++index == args.Length) throw new ArgumentException($"Missing value for {option}.");
            if (option == "--endpoint") endpoint = args[index];
            else pipeName = args[index];
        }
        else positional.Add(args[index]);
    }

    using var channel = GrpcChannel.ForAddress(endpoint);
    var client = new InputMethods.InputMethodsClient(channel);
    async Task Send(Key key, CancellationToken token) =>
        await client.PressKeyAsync(key, deadline: DateTime.UtcNow.AddSeconds(2), cancellationToken: token);

    if (positional.Count == 1 && positional[0].Equals("listen", StringComparison.OrdinalIgnoreCase))
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        await new BridgeServer(Send).RunAsync(pipeName, cancellation.Token);
    }
    else if (positional.Count == 1 && positional[0].Equals("ping", StringComparison.OrdinalIgnoreCase))
        await client.PingAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(2));
    else if (positional.Count == 2)
        await Send(BridgeServer.ParseCommand(string.Join('|', positional)), CancellationToken.None);
    else
    {
        Console.Error.WriteLine("RemoteInputBridge press|down|up <key> | ping | listen [--endpoint URL] [--pipe NAME]");
        return 2;
    }
    return 0;
}
catch (OperationCanceledException) { return 0; }
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
