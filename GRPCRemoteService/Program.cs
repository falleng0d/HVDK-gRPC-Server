using GRPCRemote.Logging;
using GRPCRemoteService;
using GRPCRemote.Configuration;

var builder = Host.CreateApplicationBuilder(args);

var logsDirectory = AppPaths.GetLogsDirectory();
builder.Logging.AddProvider(new FileLoggerProvider(logsDirectory, "grpc-remote-service"));
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GRPCRemote";
});

builder.Services.AddSingleton<WorkerSessionProcessManager>();
builder.Services.AddHostedService<SessionWorkerService>();

var host = builder.Build();
host.Run();
