using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GRPCRemoteService;

public sealed class WorkerSessionProcessManager(ILogger<WorkerSessionProcessManager> logger)
{
    private readonly object _sync = new();
    private ManagedWorkerProcess? _current;
    private DateTimeOffset _lastLaunchAttemptUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(5);

    public Task ReconcileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_current is not null && _current.HasExited)
            {
                logger.LogWarning("Observed GRPCRemote worker exit in session {SessionId} with exit code {ExitCode}", _current.SessionId, _current.ExitCode);
                _current.Dispose();
                _current = null;
            }

            var activeSessionId = NativeMethods.WTSGetActiveConsoleSessionId();
            if (activeSessionId == NativeMethods.InvalidSessionId)
            {
                if (_current is not null)
                {
                    logger.LogInformation("No active console session; stopping worker running in session {SessionId}", _current.SessionId);
                    StopCurrentWorker();
                }

                return Task.CompletedTask;
            }

            if (_current is not null)
            {
                if (_current.SessionId == activeSessionId)
                {
                    return Task.CompletedTask;
                }

                logger.LogInformation("Active console session changed from {OldSessionId} to {NewSessionId}; restarting worker", _current.SessionId, activeSessionId);
                StopCurrentWorker();
            }

            if (DateTimeOffset.UtcNow - _lastLaunchAttemptUtc < RestartBackoff)
            {
                return Task.CompletedTask;
            }

            _lastLaunchAttemptUtc = DateTimeOffset.UtcNow;
            _current = StartWorker(activeSessionId);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            StopCurrentWorker();
        }

        return Task.CompletedTask;
    }

    private ManagedWorkerProcess StartWorker(uint sessionId)
    {
        var workerPath = Path.Combine(AppContext.BaseDirectory, "worker", "GRPCRemote.exe");
        if (!File.Exists(workerPath))
        {
            throw new FileNotFoundException("Could not find GRPCRemote worker executable.", workerPath);
        }

        KillStaleWorkers(workerPath, sessionId);

        logger.LogInformation("Launching GRPCRemote worker in session {SessionId} from {WorkerPath}", sessionId, workerPath);

        using var userToken = NativeMethods.GetUserTokenForSession(sessionId);
        using var elevatedToken = NativeMethods.TryGetLinkedToken(userToken);
        using var primaryToken = NativeMethods.CreatePrimaryToken(elevatedToken ?? userToken);
        using var environment = NativeMethods.CreateEnvironmentBlock(primaryToken);

        var startupInfo = new NativeMethods.STARTUPINFO();
        startupInfo.cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>();
        startupInfo.lpDesktop = @"winsta0\default";
        startupInfo.dwFlags = NativeMethods.STARTF_USESHOWWINDOW;
        startupInfo.wShowWindow = NativeMethods.SW_HIDE;

        var processInformation = new NativeMethods.PROCESS_INFORMATION();
        var commandLine = $"\"{workerPath}\" --urls http://0.0.0.0:9036";
        var currentDirectory = AppContext.BaseDirectory;

        if (!NativeMethods.CreateProcessAsUser(
                primaryToken.DangerousGetHandle(),
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_NEW_PROCESS_GROUP | NativeMethods.CREATE_NO_WINDOW,
                environment.DangerousGetHandle(),
                currentDirectory,
                ref startupInfo,
                out processInformation))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to launch GRPCRemote in session {sessionId}.");
        }

        using var processHandle = new SafeWaitHandle(processInformation.hProcess, ownsHandle: true);
        using var threadHandle = new SafeWaitHandle(processInformation.hThread, ownsHandle: true);
        var process = Process.GetProcessById(processInformation.dwProcessId);
        process.EnableRaisingEvents = true;
        process.Exited += WorkerExited;

        logger.LogInformation("Started GRPCRemote worker process {ProcessId} in session {SessionId}", process.Id, sessionId);
        return new ManagedWorkerProcess(sessionId, process);
    }

    private void WorkerExited(object? sender, EventArgs args)
    {
        if (sender is not Process process)
        {
            return;
        }

        lock (_sync)
        {
            if (_current is null || _current.ProcessId != process.Id)
            {
                return;
            }

            logger.LogWarning("GRPCRemote worker process {ProcessId} exited in session {SessionId} with exit code {ExitCode}", _current.ProcessId, _current.SessionId, _current.ExitCode);
            _current.Dispose();
            _current = null;
            _lastLaunchAttemptUtc = DateTimeOffset.MinValue;
        }

        _ = RestartAfterExitAsync();
    }

    private async Task RestartAfterExitAsync()
    {
        try
        {
            await ReconcileAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart GRPCRemote worker after exit");
        }
    }

    private void KillStaleWorkers(string workerPath, uint activeSessionId)
    {
        foreach (var process in Process.GetProcessesByName("GRPCRemote"))
        {
            try
            {
                if (process.SessionId != activeSessionId)
                {
                    continue;
                }

                var mainModulePath = process.MainModule?.FileName;
                if (!string.Equals(mainModulePath, workerPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                logger.LogInformation("Stopping stale GRPCRemote worker process {ProcessId} in session {SessionId}", process.Id, activeSessionId);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Unable to inspect or stop candidate stale GRPCRemote worker process {ProcessId}", process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void StopCurrentWorker()
    {
        if (_current is null)
        {
            return;
        }

        try
        {
            if (!_current.HasExited)
            {
                logger.LogInformation("Stopping GRPCRemote worker process {ProcessId} in session {SessionId}", _current.ProcessId, _current.SessionId);
                _current.Process.Kill(entireProcessTree: true);
                _current.Process.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop GRPCRemote worker process {ProcessId}", _current.ProcessId);
        }
        finally
        {
            _current.Dispose();
            _current = null;
        }
    }

    private sealed class ManagedWorkerProcess(uint sessionId, Process process) : IDisposable
    {
        public uint SessionId { get; } = sessionId;
        public Process Process { get; } = process;
        public int ProcessId => Process.Id;
        public bool HasExited => Process.HasExited;
        public int? ExitCode => Process.HasExited ? Process.ExitCode : null;

        public void Dispose()
        {
            Process.Dispose();
        }
    }

    private static class NativeMethods
    {
        public const uint InvalidSessionId = 0xFFFFFFFF;
        public const int SecurityImpersonation = 2;
        public const int TokenPrimary = 1;
        public const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        public const uint TOKEN_DUPLICATE = 0x0002;
        public const uint TOKEN_QUERY = 0x0008;
        public const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        public const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        public const int TokenLinkedToken = 19;
        public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
        public const uint CREATE_NO_WINDOW = 0x08000000;
        public const int STARTF_USESHOWWINDOW = 0x00000001;
        public const short SW_HIDE = 0;

        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            SafeAccessTokenHandle existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out SafeAccessTokenHandle duplicateTokenHandle);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token, bool inherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr environment);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            SafeAccessTokenHandle tokenHandle,
            int tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessAsUser(
            IntPtr token,
            string? applicationName,
            string commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        public static SafeAccessTokenHandle GetUserTokenForSession(uint sessionId)
        {
            if (!WTSQueryUserToken(sessionId, out var token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}.");
            }

            return token;
        }

        public static SafeAccessTokenHandle CreatePrimaryToken(SafeAccessTokenHandle token)
        {
            const uint access = TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;
            if (!DuplicateTokenEx(token, access, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out var primaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to duplicate user token.");
            }

            return primaryToken;
        }

        public static SafeAccessTokenHandle? TryGetLinkedToken(SafeAccessTokenHandle token)
        {
            var buffer = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                if (!GetTokenInformation(token, TokenLinkedToken, buffer, IntPtr.Size, out _))
                {
                    return null;
                }

                var linkedHandle = Marshal.ReadIntPtr(buffer);
                return linkedHandle == IntPtr.Zero ? null : new SafeAccessTokenHandle(linkedHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static SafeEnvironmentBlockHandle CreateEnvironmentBlock(SafeAccessTokenHandle token)
        {
            if (!CreateEnvironmentBlock(out var environment, token, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create user environment block.");
            }

            return new SafeEnvironmentBlockHandle(environment);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        public sealed class SafeEnvironmentBlockHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeEnvironmentBlockHandle(IntPtr environmentHandle) : base(true)
            {
                SetHandle(environmentHandle);
            }

            protected override bool ReleaseHandle()
            {
                return DestroyEnvironmentBlock(handle);
            }
        }
    }
}
