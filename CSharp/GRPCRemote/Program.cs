using GRPCRemote.Configuration;
using GRPCRemote.Drivers;
using GRPCRemote.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(endpointOptions =>
    {
        endpointOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.Configure<RemoteHostOptions>(builder.Configuration.GetSection(RemoteHostOptions.SectionName));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RemoteHostOptions>>().Value);
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<IInputTransport>(sp =>
{
    var options = sp.GetRequiredService<RemoteHostOptions>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    return options.DriverMode.Equals("Recording", StringComparison.OrdinalIgnoreCase)
        ? new RecordingInputTransport(options, loggerFactory.CreateLogger<RecordingInputTransport>())
        : new HvdkInputTransport(options, loggerFactory.CreateLogger<HvdkInputTransport>());
});
builder.Services.AddSingleton<InputCoordinator>();
builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<InputMethodsGrpcService>();
app.MapGet("/", () => "Use a gRPC client to communicate with the remote input endpoints.");

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var inputCoordinator = scope.ServiceProvider.GetRequiredService<InputCoordinator>();
        inputCoordinator.ReleaseAllAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
    catch
    {
        // Ignore shutdown cleanup errors.
    }
});

app.Run();

public partial class Program;
