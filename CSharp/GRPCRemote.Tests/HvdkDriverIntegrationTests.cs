using GRPCRemote.Configuration;
using GRPCRemote.Drivers;
using GRPCRemote.Input;
using Microsoft.Extensions.Logging;

namespace GRPCRemote.Tests;

public sealed class HvdkDriverIntegrationTests : IDisposable
{
    private readonly RemoteHostOptions _options;
    private readonly ILogger<HvdkInputTransport> _logger;

    public HvdkDriverIntegrationTests()
    {
        _options = new RemoteHostOptions
        {
            KeyboardReportTimeoutMs = 5000,
        };
        _logger = LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<HvdkInputTransport>();
    }

    [Fact]
    public async Task Keyboard_device_connect_and_disconnect_succeeds()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        Assert.True(true);
    }

    [Fact]
    public async Task Keyboard_press_sends_down_then_up()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Keyboard_press_with_single_modifier_succeeds()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var modifier = RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift);
        var report = new KeyboardReport(modifier, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Keyboard_press_with_multiple_modifiers_succeeds()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var ctrlMask = RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl);
        var shiftMask = RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift);
        var combinedModifier = (byte)(ctrlMask | shiftMask);
        
        var report = new KeyboardReport(combinedModifier, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Theory]
    [InlineData(RemoteKey.KeyF1)]
    [InlineData(RemoteKey.KeyF2)]
    [InlineData(RemoteKey.KeyF3)]
    [InlineData(RemoteKey.KeyF10)]
    public async Task Function_keys_work(RemoteKey key)
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(key)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Caps_lock_works()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyCapital)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Tab_works()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyTab)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Fact]
    public async Task Backslash_works()
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyOemBackslash)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
    }

    [Theory]
    [InlineData(RemoteKey.Key0)]
    [InlineData(RemoteKey.Key1)]
    [InlineData(RemoteKey.Key5)]
    [InlineData(RemoteKey.Key9)]
    public async Task Number_keys_work(RemoteKey key)
    {
        using var transport = new HvdkInputTransport(_options, _logger);
        
        var report = new KeyboardReport(0, [RemoteKeyMap.GetStandardHidUsage(key)], 0);
        
        await transport.SendKeyboardAsync(report, CancellationToken.None);
        
        var releaseReport = new KeyboardReport(0, [], 0);
        await transport.SendKeyboardAsync(releaseReport, CancellationToken.None);
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

    public void Dispose()
    {
    }
}
