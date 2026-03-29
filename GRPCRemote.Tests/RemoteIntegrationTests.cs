using Grpc.Core;
using GRPCRemote.Drivers;
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
    public async Task Keyboard_press_modifier_and_hotkey_flow_emit_expected_reports()
    {
        await _fixture.ResetStateAsync();
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

        Assert.Contains(events, driverEvent => IsKeyboardEvent(0, RemoteKey.KeyA)(driverEvent));
        Assert.Contains(events, driverEvent => driverEvent.Kind == "keyboard" && driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift));
        Assert.Contains(events, driverEvent => IsKeyboardEvent(RemoteKeyMap.GetModifierMask(RemoteKey.KeyLShift), RemoteKey.KeyB)(driverEvent));
        Assert.Contains(events, driverEvent => driverEvent.Kind == "keyboard" && driverEvent.Modifier == RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl));
        Assert.Contains(events, driverEvent => IsKeyboardEvent(RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl), RemoteKey.KeyC)(driverEvent));
    }

    [Fact]
    public async Task Ping_and_simple_key_press_work_against_background_server()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        var ping = await client.PingAsync(new Empty());
        Assert.Equal("Ok", ping.Message);

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyA,
            Type = KeyActionType.Press,
        });

        var events = await _fixture.WaitForEventsAsync(2, TimeSpan.FromSeconds(5));
        var keyboardEvents = events.Where(driverEvent => driverEvent.Kind == "keyboard").Take(2).ToArray();

        Assert.Equal(2, keyboardEvents.Length);
        Assert.Equal(0, keyboardEvents[0].Modifier);
        Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA), keyboardEvents[0].Keys);
        Assert.Equal(0, keyboardEvents[1].Modifier);
        Assert.All(keyboardEvents[1].Keys, key => Assert.Equal(0, key));
    }

    [Fact]
    public async Task Multiple_keydowns_keep_existing_keys_until_explicit_release()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyA, Type = KeyActionType.Down });
        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyB, Type = KeyActionType.Down });
        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyA, Type = KeyActionType.Up });
        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyB, Type = KeyActionType.Up });

        var events = await _fixture.WaitForEventsAsync(4, TimeSpan.FromSeconds(5));

        Assert.Collection(
            events.Where(driverEvent => driverEvent.Kind == "keyboard").Take(4),
            first =>
            {
                Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA), first.Keys);
                Assert.DoesNotContain(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyB), first.Keys);
            },
            second =>
            {
                Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA), second.Keys);
                Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyB), second.Keys);
            },
            third =>
            {
                Assert.DoesNotContain(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA), third.Keys);
                Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyB), third.Keys);
            },
            fourth => Assert.All(fourth.Keys, key => Assert.Equal(0, key)));
    }

    [Fact]
    public async Task Server_recovers_after_invalid_request_and_accepts_follow_up_input()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.PressHotkeyAsync(new Hotkey
            {
                Hotkey_ = "{NotARealKey}",
                Type = KeyActionType.Down,
            }));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);

        await client.PressKeyAsync(new Key
        {
            Id = (int)RemoteKey.KeyB,
            Type = KeyActionType.Press,
        });

        var events = await _fixture.WaitForEventsAsync(2, TimeSpan.FromSeconds(5));
        Assert.Contains(events, driverEvent => IsKeyboardEvent(0, RemoteKey.KeyB)(driverEvent));
    }

    [Fact]
    public async Task Pressing_more_than_six_keys_returns_failed_precondition()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        var keys = new[]
        {
            RemoteKey.KeyA,
            RemoteKey.KeyB,
            RemoteKey.KeyC,
            RemoteKey.KeyD,
            RemoteKey.KeyE,
            RemoteKey.KeyF,
        };

        foreach (var key in keys)
        {
            await client.PressKeyAsync(new Key { Id = (int)key, Type = KeyActionType.Down });
        }

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyG, Type = KeyActionType.Down }));

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
        Assert.Contains("Cannot press more than 6 keys", exception.Status.Detail);
    }

    [Fact]
    public async Task Invalid_hotkey_returns_invalid_argument()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.PressHotkeyAsync(new Hotkey
            {
                Hotkey_ = "{DefinitelyNotAKey}",
                Type = KeyActionType.Down,
            }));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task Mouse_press_move_release_and_absolute_move_emit_expected_reports()
    {
        await _fixture.ResetStateAsync();
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
            X = 24,
            Y = 48,
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
            driverEvent.Relative &&
            driverEvent.Buttons == 0 &&
            driverEvent.X == 127 &&
            driverEvent.Y == 127);
    }

    [Fact]
    public async Task Mouse_press_action_emits_down_then_up()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.PressMouseKeyAsync(new MouseKey
        {
            Id = (int)RemoteMouseButton.Right,
            Type = MouseKey.Types.KeyActionType.Press,
        });

        var events = await _fixture.WaitForEventsAsync(2, TimeSpan.FromSeconds(5));
        var mouseEvents = events.Where(driverEvent => driverEvent.Kind == "mouse").Take(2).ToArray();

        Assert.Equal(2, mouseEvents.Length);
        Assert.Equal(RemoteKeyMap.GetMouseButtonMask(RemoteMouseButton.Right), mouseEvents[0].Buttons);
        Assert.Equal(0, mouseEvents[1].Buttons);
    }

    [Fact]
    public async Task Get_config_returns_updated_values_and_accepts_zero()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.SetConfigAsync(new Config
        {
            CursorSpeed = 0f,
            CursorAcceleration = 0f,
        });

        var config = await client.GetConfigAsync(new Empty());

        Assert.Equal(0f, config.CursorSpeed);
        Assert.Equal(0f, config.CursorAcceleration);
    }

    [Fact]
    public async Task Config_persists_across_server_restart()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.SetConfigAsync(new Config
        {
            CursorSpeed = 1.25f,
            CursorAcceleration = 0.75f,
        });

        await _fixture.RestartServerAsync();

        client = _fixture.CreateClient();
        var config = await client.GetConfigAsync(new Empty());

        Assert.Equal(1.25f, config.CursorSpeed);
        Assert.Equal(0.75f, config.CursorAcceleration);
    }

    [Fact]
    public async Task Hotkey_sequence_emits_ordered_modifier_then_key_then_release_reports()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.PressHotkeyAsync(new Hotkey
        {
            Hotkey_ = "{LCTRL Down}x{LCTRL Up}",
            Type = KeyActionType.Down,
        });

        var events = await _fixture.WaitForEventsAsync(4, TimeSpan.FromSeconds(5));
        var keyboardEvents = events.Where(driverEvent => driverEvent.Kind == "keyboard").Take(4).ToArray();

        Assert.Equal(4, keyboardEvents.Length);
        Assert.Equal(RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl), keyboardEvents[0].Modifier);
        Assert.All(keyboardEvents[0].Keys, key => Assert.Equal(0, key));

        Assert.Equal(RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl), keyboardEvents[1].Modifier);
        Assert.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyX), keyboardEvents[1].Keys);

        Assert.Equal(RemoteKeyMap.GetModifierMask(RemoteKey.KeyLControl), keyboardEvents[2].Modifier);
        Assert.All(keyboardEvents[2].Keys, key => Assert.Equal(0, key));

        Assert.Equal(0, keyboardEvents[3].Modifier);
        Assert.All(keyboardEvents[3].Keys, key => Assert.Equal(0, key));
    }

    [Fact]
    public async Task Restarting_server_resets_keyboard_and_mouse_state()
    {
        await _fixture.ResetStateAsync();
        var client = _fixture.CreateClient();

        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyLShift, Type = KeyActionType.Down });
        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyA, Type = KeyActionType.Down });
        await client.PressMouseKeyAsync(new MouseKey { Id = (int)RemoteMouseButton.Left, Type = MouseKey.Types.KeyActionType.Down });

        await _fixture.RestartServerAsync();
        await _fixture.ClearRecordingsAsync();

        client = _fixture.CreateClient();
        await client.PressKeyAsync(new Key { Id = (int)RemoteKey.KeyA, Type = KeyActionType.Press });
        await client.MoveMouseAsync(new MouseMove { X = 1, Y = 1, Relative = true });

        var events = await _fixture.WaitForEventsAsync(3, TimeSpan.FromSeconds(5));
        var keyboardEvents = events.Where(driverEvent => driverEvent.Kind == "keyboard").ToArray();
        var mouseEvent = Assert.Single(events.Where(driverEvent => driverEvent.Kind == "mouse"));

        Assert.NotEmpty(keyboardEvents);
        Assert.All(keyboardEvents, driverEvent => Assert.Equal(0, driverEvent.Modifier));
        Assert.Contains(keyboardEvents, driverEvent => driverEvent.Keys.Contains(RemoteKeyMap.GetStandardHidUsage(RemoteKey.KeyA)));

        Assert.True(mouseEvent.Relative);
        Assert.Equal(0, mouseEvent.Buttons);
        Assert.Equal(5, mouseEvent.X);
        Assert.Equal(5, mouseEvent.Y);
    }

    private static Predicate<DriverEvent> IsKeyboardEvent(byte modifier, RemoteKey key)
    {
        var usage = RemoteKeyMap.GetStandardHidUsage(key);
        return driverEvent =>
            driverEvent.Kind == "keyboard" &&
            driverEvent.Modifier == modifier &&
            driverEvent.Keys.Contains(usage);
    }
}
