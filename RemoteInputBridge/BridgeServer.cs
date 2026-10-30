using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Grpc.Core;
using GRPCRemote;
using GRPCRemote.Input;
using Microsoft.Win32.SafeHandles;

namespace RemoteInputBridge;

public sealed class BridgeServer(Func<Key, CancellationToken, Task> send)
{
    public static Key ParseCommand(string command)
    {
        var parts = command.Split('|');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            throw new FormatException("Expected action|key.");
        var action = parts[0].Trim().ToLowerInvariant() switch
        {
            "press" => KeyActionType.Press,
            "down" => KeyActionType.Down,
            "up" => KeyActionType.Up,
            _ => throw new ArgumentException("Use press, down, or up.")
        };
        var token = parts[1].Trim();
        var key = int.TryParse(token, out var id) ? RemoteKeyMap.FromId(id) : RemoteKeyMap.ParseKey(token);
        return new Key { Id = (int)key, Type = action };
    }

    public async Task RunAsync(string pipeName, CancellationToken cancellationToken)
    {
        NamedPipeServerStream listener;
        try { listener = CreatePipe(pipeName, first: true); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5) { return; }
        var clients = new HashSet<Task>();
        try
        {
            while (true)
            {
                await listener.WaitForConnectionAsync(cancellationToken);
                var connected = listener;
                // Keep a pipe instance alive while transferring ownership to a client handler.
                listener = CreatePipe(pipeName, first: false);
                clients.RemoveWhere(task => task.IsCompleted);
                clients.Add(ServeAsync(connected, cancellationToken));
            }
        }
        finally
        {
            listener.Dispose();
            await Task.WhenAll(clients);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await using var connection = pipe;
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        try
        {
            while (await reader.ReadLineAsync(token) is { } command)
            {
                string reply;
                try
                {
                    await send(ParseCommand(command), token);
                    reply = "ok";
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException or RpcException or IOException)
                {
                    reply = "error " + ex.Message.Replace('\r', ' ').Replace('\n', ' ');
                }
                await writer.WriteLineAsync(reply.AsMemory(), token);
            }
        }
        catch (IOException) { }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static NamedPipeServerStream CreatePipe(string name, bool first)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Named pipe hosting requires Windows.");
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User!.Value;
        var sddl = $"D:P(D;;GA;;;NU)(A;;GA;;;{sid})S:(ML;;NW;;;ME)";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out var descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            var handle = CreateNamedPipe(@"\\.\pipe\" + name, 3u | 0x40000000u | (first ? 0x00080000u : 0), 8, 255, 4096, 4096, 0, ref attributes);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }
            return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
        }
        finally { LocalFree(descriptor); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        public int InheritHandle;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode, uint maxInstances, uint outBufferSize, uint inBufferSize, uint timeout, ref SecurityAttributes attributes);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
