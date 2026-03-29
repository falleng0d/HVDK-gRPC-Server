using System.Runtime.InteropServices;
using GRPCRemote.Input;

namespace GRPCRemote.Services;

public sealed class VirtualKeyService
{
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public async Task SendMediaKeyAsync(RemoteKey key, RemoteActionType action, CancellationToken cancellationToken)
    {
        var vkCode = RemoteKeyMap.GetMediaKeyVkCode(key);
        var isDown = action == RemoteActionType.Down || action == RemoteActionType.Press;
        var flags = isDown ? 0 : KEYEVENTF_KEYUP;

        keybd_event((byte)vkCode, 0, flags, UIntPtr.Zero);

        if (action == RemoteActionType.Press)
        {
            await Task.Delay(50, cancellationToken);
            keybd_event((byte)vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
