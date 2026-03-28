using GRPCRemote.Input;

namespace GRPCRemote.Tests;

[Collection(IntegrationCollection.Name)]
public sealed class RemoteIntegrationTests
{
    private readonly GrpcRemoteServerFixture _fixture;

    public RemoteIntegrationTests(GrpcRemoteServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Keyboard_and_hotkey_requests_emit_expected_keyboard_reports()
    {
        await _fixture.ClearRecordingsAsync();
        var client = _fixture.CreateClient();

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyA,
            Type = KeyActionType.Press,
        });

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyLShift,
            Type = KeyActionType.Down,
        });

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyB,
            Type = KeyActionType.Press,
        });

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyLShift,
            Type = KeyActionType.Up,
        });

        await client.PressHotkeyAsync(new Hotkey
        {
            Hotkey_ = "{LCTRL Down}c{LCTRL Up}",
            Type = KeyActionType.Down,
        });

        var events = await _fixture.WaitForEventsAsync(8, TimeSpan.FromSeconds(5));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == 0 &&
            driverEvent.Keys.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift) &&
            driverEvent.Keys.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyB)));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl) &&
            driverEvent.Keys.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyC)));
    }

    [Fact]
    public async Task Mouse_and_config_requests_emit_expected_mouse_reports()
    {
        await _fixture.ClearRecordingsAsync();
        var client = _fixture.CreateClient();

        var updatedConfig = await client.SetConfigAsync(new Config
        {
            CursorSpeed = 1.5f,
            CursorAcceleration = 0.5f,
        });

        Assert.Equal(1.5f, updatedConfig.CursorSpeed);
        Assert.Equal(0.5f, updatedConfig.CursorAcceleration);

        await client.PressMouseKeyAsync(new MouseKey
        {
            Id = (int)RemoteMouseButton.Left,
            Type = MouseKey.Types.KeyActionType.Down,
        });

        await client.MoveMouseAsync(new MouseMove
        {
            X = 2,
            Y = -1,
            Relative = true,
        });

        await client.PressMouseKeyAsync(new MouseKey
        {
            Id = (int)RemoteMouseButton.Left,
            Type = MouseKey.Types.KeyActionType.Up,
        });

        await client.MoveMouseAsync(new MouseMove
        {
            X = 120,
            Y = 240,
            Relative = false,
        });

        var events = await _fixture.WaitForEventsAsync(4, TimeSpan.FromSeconds(5));

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "mouse" &&
            driverEvent.Relative &&
            driverEvent.Buttons == RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Left) &&
            driverEvent.X == 0 &&
            driverEvent.Y == 0);

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "mouse" &&
            driverEvent.Relative &&
            driverEvent.Buttons == RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Left) &&
            driverEvent.X == 15 &&
            driverEvent.Y == -8);

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "mouse" &&
            driverEvent.Relative &&
            driverEvent.Buttons == 0 &&
            driverEvent.X == 0 &&
            driverEvent.Y == 0);

        Assert.Contains(events, driverEvent =>
            driverEvent.Kind == "mouse" &&
            !driverEvent.Relative &&
            driverEvent.Buttons == 0 &&
            driverEvent.X == 120 &&
            driverEvent.Y == 240);
    }
}
