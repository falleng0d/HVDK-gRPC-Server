extern alias bridge;

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using BridgeServer = bridge::RemoteInputBridge.BridgeServer;
using BridgeMethods = bridge::GRPCRemote.InputMethods;

namespace GRPCRemote.Tests;

public class RemoteInputBridgeTests
{
    [Theory]
    [InlineData("down|L", 21, KeyActionType.Down)]
    [InlineData("UP| rctrl ", 56, KeyActionType.Up)]
    [InlineData("press|PAGE_UP", 66, KeyActionType.Press)]
    [InlineData("press|21", 21, KeyActionType.Press)]
    [InlineData("press|LCONTROL", 55, KeyActionType.Press)]
    public void Commands_use_server_key_ids(string command, int id, KeyActionType action)
    {
        var key = BridgeServer.ParseCommand(command);
        Assert.Equal(id, key.Id);
        Assert.Equal((int)action, (int)key.Type);
    }

    [Theory]
    [InlineData("down")]
    [InlineData("down|")]
    [InlineData("press|L|up")]
    [InlineData("typo|L")]
    [InlineData("press|122")]
    [InlineData("press|unknown")]
    public void Invalid_commands_are_rejected(string command)
    {
        Assert.ThrowsAny<Exception>(() => BridgeServer.ParseCommand(command));
    }

    [Fact]
    public async Task Concurrent_clients_receive_replies_and_survive_command_and_rpc_errors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0,
            listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<FakeInputService>();
        await using var app = builder.Build();
        app.MapGrpcService<FakeInputService>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var channel = GrpcChannel.ForAddress(address);
        var client = new BridgeMethods.InputMethodsClient(channel);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pipeName = "GRPCRemoteInput-test-" + Guid.NewGuid().ToString("N");
        var server = new BridgeServer(async (key, token) => await client.PressKeyAsync(key, cancellationToken: token));
        var running = server.RunAsync(pipeName, cancellation.Token);
        try
        {
            await using var first = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await first.ConnectAsync(cancellation.Token);
            using var reader = new StreamReader(first);
            await using var writer = new StreamWriter(first, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync("down|A");
            var service = app.Services.GetRequiredService<FakeInputService>();
            await service.Entered.Task.WaitAsync(cancellation.Token);

            await using var second = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await second.ConnectAsync(cancellation.Token);
            using var otherReader = new StreamReader(second);
            await using var otherWriter = new StreamWriter(second, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await otherWriter.WriteLineAsync("nonsense");
            Assert.StartsWith("error ", await otherReader.ReadLineAsync(cancellation.Token));
            await otherWriter.WriteLineAsync("press|B");
            Assert.StartsWith("error ", await otherReader.ReadLineAsync(cancellation.Token));
            await otherWriter.WriteLineAsync("up|L");
            Assert.Equal("ok", await otherReader.ReadLineAsync(cancellation.Token));
            Assert.False(service.Release.Task.IsCompleted);

            await server.RunAsync(pipeName, cancellation.Token).WaitAsync(cancellation.Token);
            service.Release.SetResult();
            Assert.Equal("ok", await reader.ReadLineAsync(cancellation.Token));
            Assert.Contains(service.Keys, key => key.Id == 21 && key.Type == KeyActionType.Up);
            Assert.Contains(service.Keys, key => key.Id == 10 && key.Type == KeyActionType.Down);
        }
        finally
        {
            app.Services.GetRequiredService<FakeInputService>().Release.TrySetResult();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            await app.StopAsync();
        }
    }

    public sealed class FakeInputService : InputMethods.InputMethodsBase
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<Key> Keys { get; } = [];

        public override async Task<Response> PressKey(Key request, ServerCallContext context)
        {
            if (request.Id == 11) throw new RpcException(new Status(StatusCode.Unavailable, "test\r\nfailure"));
            if (request.Id == 10)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(context.CancellationToken);
            }
            Keys.Add(request);
            return new Response();
        }
    }
}
