using System.Runtime.InteropServices;
using GRPCRemote.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace GRPCRemote.Services;

public sealed class VirtualKeyService
{
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private static readonly TimeSpan MediaKeyPressDuration = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan RepeatStartDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(75);

    private readonly object _sync = new();
    private readonly ILogger<VirtualKeyService> _logger;
    private readonly Action<ushort, uint> _sendKeyEvent;
    private readonly Dictionary<RemoteKey, ActiveMediaKeyRepeat> _activeRepeats = [];
    private readonly HashSet<RemoteKey> _pressedMediaKeys = [];

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public VirtualKeyService(ILogger<VirtualKeyService> logger)
        : this(logger, SendNativeKeyEvent)
    {
    }

    internal VirtualKeyService(ILogger<VirtualKeyService> logger, Action<ushort, uint> sendKeyEvent)
    {
        _logger = logger;
        _sendKeyEvent = sendKeyEvent;
    }

    internal VirtualKeyService(Action<ushort, uint> sendKeyEvent)
        : this(NullLogger<VirtualKeyService>.Instance, sendKeyEvent)
    {
    }

    public async Task SendMediaKeyAsync(RemoteKey key, RemoteActionType action, CancellationToken cancellationToken)
    {
        if (IsRepeatableMediaKey(key))
        {
            switch (action)
            {
                case RemoteActionType.Down:
                    await StartMediaKeyRepeatAsync(key, cancellationToken);
                    return;
                case RemoteActionType.Up:
                    await StopMediaKeyRepeatAsync(key);
                    return;
            }
        }

        switch (action)
        {
            case RemoteActionType.Down:
                SendHeldMediaKeyDown(key);
                return;
            case RemoteActionType.Up:
                SendHeldMediaKeyUp(key);
                return;
            default:
                await SendMediaKeyPressAsync(key, cancellationToken);
                return;
        }
    }

    public async Task ReleaseAllAsync()
    {
        ActiveMediaKeyRepeat[] repeats;
        RemoteKey[] pressedKeys;

        lock (_sync)
        {
            repeats = _activeRepeats.Values.ToArray();
            _activeRepeats.Clear();
            pressedKeys = _pressedMediaKeys.ToArray();
            _pressedMediaKeys.Clear();
        }

        foreach (var repeat in repeats)
        {
            repeat.Cancellation.Cancel();
        }

        foreach (var repeat in repeats)
        {
            try
            {
                await repeat.Execution.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when a held repeat is released.
            }
        }

        foreach (var key in pressedKeys)
        {
            SendMediaKeyEvent(key, KEYEVENTF_KEYUP);
        }
    }

    private async Task StartMediaKeyRepeatAsync(RemoteKey key, CancellationToken cancellationToken)
    {
        ActiveMediaKeyRepeat? repeat;

        lock (_sync)
        {
            if (_activeRepeats.ContainsKey(key))
            {
                return;
            }

            var repeatCts = new CancellationTokenSource();
            repeat = new ActiveMediaKeyRepeat(
                repeatCts,
                RepeatMediaKeyUntilReleasedAsync(key, repeatCts));
            _activeRepeats[key] = repeat;
        }

        try
        {
            await SendMediaKeyPressAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await StopMediaKeyRepeatAsync(key).ConfigureAwait(false);
            throw;
        }
    }

    private async Task StopMediaKeyRepeatAsync(RemoteKey key)
    {
        ActiveMediaKeyRepeat? repeat;

        lock (_sync)
        {
            if (!_activeRepeats.TryGetValue(key, out repeat))
            {
                return;
            }

            _activeRepeats.Remove(key);
        }

        repeat.Cancellation.Cancel();

        try
        {
            await repeat.Execution.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when a held repeat is released.
        }
    }

    private async Task RepeatMediaKeyUntilReleasedAsync(RemoteKey key, CancellationTokenSource repeatCts)
    {
        try
        {
            await Task.Delay(RepeatStartDelay, repeatCts.Token).ConfigureAwait(false);

            while (!repeatCts.IsCancellationRequested)
            {
                await SendMediaKeyPressAsync(key, repeatCts.Token).ConfigureAwait(false);
                await Task.Delay(RepeatInterval, repeatCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (repeatCts.IsCancellationRequested)
        {
            // Expected when a held repeat is released.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure while repeating media key {Key}", key);
        }
        finally
        {
            repeatCts.Dispose();

            lock (_sync)
            {
                if (_activeRepeats.TryGetValue(key, out var active) && ReferenceEquals(active.Cancellation, repeatCts))
                {
                    _activeRepeats.Remove(key);
                }
            }
        }
    }

    private void SendHeldMediaKeyDown(RemoteKey key)
    {
        lock (_sync)
        {
            if (!_pressedMediaKeys.Add(key))
            {
                return;
            }
        }

        SendMediaKeyEvent(key, 0);
    }

    private void SendHeldMediaKeyUp(RemoteKey key)
    {
        lock (_sync)
        {
            _pressedMediaKeys.Remove(key);
        }

        SendMediaKeyEvent(key, KEYEVENTF_KEYUP);
    }

    private async Task SendMediaKeyPressAsync(RemoteKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SendMediaKeyEvent(key, 0);

        try
        {
            await Task.Delay(MediaKeyPressDuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Preserve the balancing key-up on cancellation.
        }
        finally
        {
            SendMediaKeyEvent(key, KEYEVENTF_KEYUP);
        }
    }

    private void SendMediaKeyEvent(RemoteKey key, uint flags)
    {
        _sendKeyEvent(RemoteKeyMap.GetMediaKeyVkCode(key), flags);
    }

    private static void SendNativeKeyEvent(ushort vkCode, uint flags)
    {
        keybd_event((byte)vkCode, 0, flags, UIntPtr.Zero);
    }

    private static bool IsRepeatableMediaKey(RemoteKey key)
    {
        return key is RemoteKey.KeyVolumeUp or RemoteKey.KeyVolumeDown;
    }

    private sealed record ActiveMediaKeyRepeat(CancellationTokenSource Cancellation, Task Execution);
}
