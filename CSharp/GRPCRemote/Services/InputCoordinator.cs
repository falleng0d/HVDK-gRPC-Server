using GRPCRemote.Configuration;
using GRPCRemote.Drivers;
using GRPCRemote.Input;

namespace GRPCRemote.Services;

public sealed class InputCoordinator
{
    private readonly object _sync = new();
    private readonly ConfigService _configService;
    private readonly IInputTransport _transport;
    private readonly RemoteHostOptions _options;
    private readonly ILogger<InputCoordinator> _logger;
    private readonly byte[] _pressedKeys = new byte[6];
    private byte _modifierState;
    private byte _mouseButtons;

    public InputCoordinator(
        ConfigService configService,
        IInputTransport transport,
        RemoteHostOptions options,
        ILogger<InputCoordinator> logger)
    {
        _configService = configService;
        _transport = transport;
        _options = options;
        _logger = logger;
    }

    public async Task PressKeyAsync(RemoteKey key, RemoteActionType action, KeyRequestOptions options, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PressKey {Key} {Action}", key, action);

        if (action == RemoteActionType.Press)
        {
            await ApplyKeyStateAsync(key, RemoteActionType.Down, cancellationToken);
            await Task.Delay(_configService.Snapshot.KeyPressInterval, cancellationToken);
            await ApplyKeyStateAsync(key, RemoteActionType.Up, cancellationToken);
            return;
        }

        await ApplyKeyStateAsync(key, action, cancellationToken);
    }

    public async Task PressHotkeyAsync(string hotkey, RemoteActionType action, HotkeyRequestOptions options, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PressHotkey {Hotkey} {Action}", hotkey, action);

        if (action == RemoteActionType.Up)
        {
            return;
        }

        foreach (var step in HotkeyParser.Parse(hotkey))
        {
            await Task.Delay(step.WaitMilliseconds.GetValueOrDefault(_configService.Snapshot.KeyPressInterval), cancellationToken);

            await ApplyKeyStateAsync(step.Key, step.Action, cancellationToken);
        }
    }

    public Task PressMouseKeyAsync(RemoteMouseButton button, RemoteActionType action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PressMouseKey {Button} {Action}", button, action);

        if (action == RemoteActionType.Press)
        {
            return PressMouseButtonAsync(button, cancellationToken);
        }

        var report = ApplyMouseButtonState(button, action);
        return _transport.SendRelativeMouseAsync(report, cancellationToken);
    }

    public Task MoveMouseAsync(float x, float y, bool relative, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MoveMouse X={X} Y={Y} Relative={Relative}", x, y, relative);
        var config = _configService.Snapshot;

        if (relative)
        {
            var scaledX = ClampToSByte((int)Math.Floor(x * config.CursorSpeed * 5f));
            var scaledY = ClampToSByte((int)Math.Floor(y * config.CursorSpeed * 5f));

            RelativeMouseReport report;
            lock (_sync)
            {
                report = new RelativeMouseReport(_mouseButtons, scaledX, scaledY);
            }

            return _transport.SendRelativeMouseAsync(report, cancellationToken);
        }

        AbsoluteMouseReport absoluteReport;
        lock (_sync)
        {
            absoluteReport = new AbsoluteMouseReport(_mouseButtons, ClampToUInt16(x), ClampToUInt16(y));
        }

        return _transport.SendAbsoluteMouseAsync(absoluteReport, cancellationToken);
    }

    public async Task ReleaseAllAsync(CancellationToken cancellationToken)
    {
        KeyboardReport keyboardReport;
        RelativeMouseReport mouseReport;

        lock (_sync)
        {
            Array.Clear(_pressedKeys);
            _modifierState = 0;
            _mouseButtons = 0;
            keyboardReport = CreateKeyboardReport();
            mouseReport = new RelativeMouseReport(_mouseButtons, 0, 0);
        }

        await _transport.SendKeyboardAsync(keyboardReport, cancellationToken);
        await _transport.SendRelativeMouseAsync(mouseReport, cancellationToken);
    }

    private async Task ApplyKeyStateAsync(RemoteKey key, RemoteActionType action, CancellationToken cancellationToken)
    {
        KeyboardReport report;

        lock (_sync)
        {
            if (RemoteKeyMap.IsModifier(key))
            {
                var mask = RemoteKeyMap.GetModifierMask(key);
                if (action == RemoteActionType.Down)
                {
                    _modifierState |= mask;
                }
                else if (action == RemoteActionType.Up)
                {
                    _modifierState = (byte)(_modifierState & ~mask);
                }
            }
            else
            {
                var usage = RemoteKeyMap.GetStandardHidUsage(key);
                if (action == RemoteActionType.Down)
                {
                    AddPressedKey(usage);
                }
                else if (action == RemoteActionType.Up)
                {
                    RemovePressedKey(usage);
                }
            }

            report = CreateKeyboardReport();
        }
        
        _logger.LogDebug("Sending keyboard report to transport: Modifier={Modifier}, Keys=[{Keys}]", report.Modifier,
            string.Join(",", report.Keys));

        await _transport.SendKeyboardAsync(report, cancellationToken);
    }

    private async Task PressMouseButtonAsync(RemoteMouseButton button, CancellationToken cancellationToken)
    {
        var down = ApplyMouseButtonState(button, RemoteActionType.Down);
        await _transport.SendRelativeMouseAsync(down, cancellationToken);
        await Task.Delay(150, cancellationToken);
        var up = ApplyMouseButtonState(button, RemoteActionType.Up);
        await _transport.SendRelativeMouseAsync(up, cancellationToken);
    }

    private RelativeMouseReport ApplyMouseButtonState(RemoteMouseButton button, RemoteActionType action)
    {
        lock (_sync)
        {
            var mask = RemoteKeyMap.GetMouseButtonMask(button);
            if (action == RemoteActionType.Down)
            {
                _mouseButtons |= mask;
            }
            else if (action == RemoteActionType.Up)
            {
                _mouseButtons = (byte)(_mouseButtons & ~mask);
            }

            return new RelativeMouseReport(_mouseButtons, 0, 0);
        }
    }

    private KeyboardReport CreateKeyboardReport()
    {
        return new KeyboardReport(_modifierState, _pressedKeys.ToArray(), (uint)_options.KeyboardReportTimeoutMs);
    }

    private void AddPressedKey(byte usage)
    {
        if (_pressedKeys.Contains(usage))
        {
            return;
        }

        for (var i = 0; i < _pressedKeys.Length; i++)
        {
            if (_pressedKeys[i] == 0)
            {
                _pressedKeys[i] = usage;
                return;
            }
        }

        throw new InvalidOperationException("Cannot press more than 6 keys at once.");
    }

    private void RemovePressedKey(byte usage)
    {
        for (var i = 0; i < _pressedKeys.Length; i++)
        {
            if (_pressedKeys[i] == usage)
            {
                _pressedKeys[i] = 0;
            }
        }
    }

    private static sbyte ClampToSByte(int value)
    {
        return (sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
    }

    private static ushort ClampToUInt16(float value)
    {
        return (ushort)Math.Clamp((int)Math.Round(value), ushort.MinValue, ushort.MaxValue);
    }
}
