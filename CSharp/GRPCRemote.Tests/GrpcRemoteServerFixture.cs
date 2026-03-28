using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Grpc.Net.Client;
using GRPCRemote.Drivers;

namespace GRPCRemote.Tests;

public sealed class GrpcRemoteServerFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private Process? _process;
    private string _eventLogPath = string.Empty;
    private string _configPath = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var port = GetFreeTcpPort();
        Address = $"http://127.0.0.1:{port}";

        var solutionRoot = FindSolutionRoot();
        var configuration = FindBuildConfiguration();
        var serverDllPath = Path.Combine(solutionRoot, "GRPCRemote", "bin", configuration, "net8.0", "GRPCRemote.dll");
        _eventLogPath = Path.Combine(Path.GetTempPath(), $"grpc-remote-events-{Guid.NewGuid():N}.jsonl");
        _configPath = Path.Combine(Path.GetTempPath(), $"grpc-remote-config-{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(serverDllPath)!,
        };
        startInfo.ArgumentList.Add(serverDllPath);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(Address);
        startInfo.Environment["GRPCRemote__DriverMode"] = "Recording";
        startInfo.Environment["GRPCRemote__RecordingPath"] = _eventLogPath;
        startInfo.Environment["GRPCRemote__ConfigPath"] = _configPath;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += (_, args) => AppendLine(_stdout, args.Data);
        _process.ErrorDataReceived += (_, args) => AppendLine(_stderr, args.Data);

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start GRPCRemote server process.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForServerAsync();
    }

    public async Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();

        TryDelete(_eventLogPath);
        TryDelete(_configPath);
    }

    public InputMethods.InputMethodsClient CreateClient()
    {
        var channel = GrpcChannel.ForAddress(Address);
        return new InputMethods.InputMethodsClient(channel);
    }

    public Task ClearRecordingsAsync()
    {
        File.WriteAllText(_eventLogPath, string.Empty);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DriverEvent>> WaitForEventsAsync(int minimumCount, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var events = await ReadEventsAsync();
            if (events.Count >= minimumCount)
            {
                return events;
            }

            await Task.Delay(100);
        }

        return await ReadEventsAsync();
    }

    private async Task WaitForServerAsync()
    {
        var started = DateTime.UtcNow;
        Exception? lastException = null;

        while (DateTime.UtcNow - started < TimeSpan.FromSeconds(20))
        {
            if (_process is { HasExited: true })
            {
                break;
            }

            try
            {
                using var channel = GrpcChannel.ForAddress(Address);
                var client = new InputMethods.InputMethodsClient(channel);
                await client.PingAsync(new Empty());
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(200);
            }
        }

        throw new TimeoutException($"Timed out waiting for GRPCRemote server. Last error: {lastException}\nSTDOUT:\n{_stdout}\nSTDERR:\n{_stderr}");
    }

    private async Task<IReadOnlyList<DriverEvent>> ReadEventsAsync()
    {
        if (!File.Exists(_eventLogPath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(_eventLogPath);
        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<DriverEvent>(line, JsonOptions))
            .Where(driverEvent => driverEvent is not null)
            .Cast<DriverEvent>()
            .ToArray();
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Utils.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Utils.sln from the test output directory.");
    }

    private static string FindBuildConfiguration()
    {
        var frameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        return frameworkDirectory.Parent?.Name ?? "Debug";
    }

    private static void AppendLine(StringBuilder builder, string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            builder.AppendLine(line);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}
