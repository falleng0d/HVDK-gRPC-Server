using GRPCRemote.Logging;
using GRPCRemoteService;

var builder = Host.CreateApplicationBuilder(args);

var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
builder.Logging.AddProvider(new FileLoggerProvider(logsDirectory, "grpc-remote-service"));
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GRPCRemote";
});

builder.Services.AddSingleton<WorkerSessionProcessManager>();
builder.Services.AddHostedService<SessionWorkerService>();

var host = builder.Build();
host.Run();
