using System.ServiceProcess;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace GRPCRemoteService;

public sealed class SessionWorkerService(
    WorkerSessionProcessManager processManager,
    ILogger<SessionWorkerService> logger) : BackgroundService
{
    private TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Session worker service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processManager.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reconcile GRPCRemote worker state");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        await processManager.StopAsync(CancellationToken.None);
        logger.LogInformation("Session worker service stopped");
    }
}
