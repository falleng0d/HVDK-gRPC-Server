using System.Text.Json;
using GRPCRemote.Configuration;

namespace GRPCRemote.Drivers;

public sealed class RecordingInputTransport : IInputTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<RecordingInputTransport> _logger;
    private readonly string _recordingPath;
    private bool _disposed;

    public RecordingInputTransport(RemoteHostOptions options, ILogger<RecordingInputTransport> logger)
    {
        _logger = logger;
        _recordingPath = AppPaths.ResolveRecordingPath(options.RecordingPath);
        
        logger.LogInformation("Recording input events to {Path}", _recordingPath);

        var directory = Path.GetDirectoryName(_recordingPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public Task SendKeyboardAsync(KeyboardReport report, CancellationToken cancellationToken)
    {
        return RecordAsync(new DriverEvent
        {
            Kind = "keyboard",
            Modifier = report.Modifier,
            Keys = report.Keys.ToArray(),
        }, cancellationToken);
    }

    public Task SendRelativeMouseAsync(RelativeMouseReport report, CancellationToken cancellationToken)
    {
        return RecordAsync(new DriverEvent
        {
            Kind = "mouse",
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
            Relative = true,
        }, cancellationToken);
    }

    public Task SendAbsoluteMouseAsync(AbsoluteMouseReport report, CancellationToken cancellationToken)
    {
        return RecordAsync(new DriverEvent
        {
            Kind = "mouse",
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
            Relative = false,
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }

    private async Task RecordAsync(DriverEvent driverEvent, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = new FileStream(_recordingPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream);
            var line = JsonSerializer.Serialize(driverEvent, JsonOptions);
            await writer.WriteLineAsync(line);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogDebug("Recorded {Kind} event to {Path}", driverEvent.Kind, _recordingPath);
    }
}
