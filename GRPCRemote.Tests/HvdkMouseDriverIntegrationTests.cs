using GRPCRemote.Configuration;
using GRPCRemote.Drivers;
using GRPCRemote.Input;
using Microsoft.Extensions.Logging;

namespace GRPCRemote.Tests;

public sealed class HvdkMouseDriverIntegrationTests : IDisposable
{
    private readonly RemoteHostOptions _options;
    private readonly ILogger<HvdkInputTransport> _logger;

    public HvdkMouseDriverIntegrationTests()
    {
        _options = new RemoteHostOptions
        {
            DriverMode = "Real",
            KeyboardReportTimeoutMs = 5000,
        };
        _logger = LoggerFactory.Create(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        }).CreateLogger<HvdkInputTransport>();
    }

    [Fact]
    public async Task Mouse_device_connect_and_disconnect_succeeds()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new RelativeMouseReport(0, 0, 0);
        
        await transport.SendRelativeMouseAsync(report, CancellationToken.None);
    }

    [Fact]
    public async Task Mouse_button_press_sends_down_then_up()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var buttonMask = RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Left);
        var pressReport = new RelativeMouseReport(buttonMask, 0, 0);
        
        await transport.SendRelativeMouseAsync(pressReport, CancellationToken.None);
        
        var releaseReport = new RelativeMouseReport(0, 0, 0);
        await transport.SendRelativeMouseAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Mouse_right_button_press_sends_down_then_up()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var buttonMask = RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Right);
        var pressReport = new RelativeMouseReport(buttonMask, 0, 0);
        
        await transport.SendRelativeMouseAsync(pressReport, CancellationToken.None);
        
        var releaseReport = new RelativeMouseReport(0, 0, 0);
        await transport.SendRelativeMouseAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Mouse_relative_movement_works()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new RelativeMouseReport(0, 10, -5);
        
        await transport.SendRelativeMouseAsync(report, CancellationToken.None);
    }

    [Fact]
    public async Task Mouse_combined_button_and_movement_works()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var buttonMask = RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Left);
        var report = new RelativeMouseReport(buttonMask, 20, 15);
        
        await transport.SendRelativeMouseAsync(report, CancellationToken.None);
        
        var releaseReport = new RelativeMouseReport(0, 0, 0);
        await transport.SendRelativeMouseAsync(releaseReport, CancellationToken.None);
    }

    public void Dispose() {}
}
