# HVDK gRPC Server

GRPCRemote is a gRPC server that exposes the same API as the Python `pi-remote`
implementation, but uses the HVDK driver for hardware injection instead of the
original custom/pi-remote driver. It allows external clients to send keyboard,
mouse, and hotkey commands that get translated into HID reports and sent to
virtual HID devices.

## Overview

- **Purpose**: Provide a network-accessible gRPC API for injecting keyboard and mouse
  input using Windows HVDK drivers
- **Reference**: The Python `pi-remote` server in the parent directory defines the
  gRPC contract
- **Architecture**: ASP.NET Core gRPC server with pluggable input transports
- **Binding**: Server binds to `0.0.0.0:9036` to accept external connections
- **Driver**: HVDK (Tetherscript Virtual HID Driver Kit)

## Driver Communication

GRPCRemote communicates with HVDK drivers through:

1. **Common/HIDCtrl.cs** - Low-level HID device control (linked into GRPCRemote)
2. **Common/Drivers.cs** - Driver struct definitions

The server uses an `IInputTransport` abstraction:
- `HvdkInputTransport` - Real HVDK driver communication (production use)
- `RecordingInputTransport` - Test mode that records events for verification

Key classes:
- `GRPCRemote/Drivers/Reports.cs` - HID report structures (KeyboardReport, MouseReport)
- `GRPCRemote/Drivers/HvdkInputTransport.cs` - Sends reports to HVDK devices
- `GRPCRemote/Input/RemoteKeyMap.cs` - Maps key names to HID codes

## AutoHotkey v1 remote input

The MSI installs `RemoteInputBridge.exe` and `remote_input.ahk` in
`C:\Program Files\GRPCRemote` alongside `GRPCRemoteService.exe` (the server worker
is in `worker`). With 64-bit AutoHotkey v1.1.28 or later, use:

```autohotkey
#Include %A_ProgramFiles%\GRPCRemote\remote_input.ahk

if (!RemoteInputInitialize())
    ExitApp
RemoteInputDown("L")
RemoteInputUp("L")
RemoteInputPress("ENTER")
```

Use 64-bit AutoHotkey so `A_ProgramFiles` selects the 64-bit install directory.
For a custom MSI install directory, use its absolute helper path in `#Include`
without quotes. The helper locates the executable beside its own `A_LineFile`;
`GRPC_REMOTE_INPUT_BRIDGE` overrides that executable path.

Each script keeps a duplex connection to the local `GRPCRemoteInput` named pipe.
Requests are UTF-8 `press|key`, `down|key`, or `up|key` lines; each receives `ok`
after gRPC completion or a single-line `error <message>`. Helpers return false
on errors and expose `RemoteInputLastError`. gRPC calls have a two-second deadline;
the helper stops waiting after three seconds and reconnects on the next call.
Commands are never automatically replayed after an uncertain result.

One shared bridge stays running until explicitly stopped or Windows signs out.
Scripts close only their own pipe on exit. Concurrent startup losers exit cleanly;
connections run independently. Pipe access is limited to the same Windows user,
including elevated and unelevated processes, and rejects network clients. Scripts
share the server's keyboard state: callers must coordinate ownership of held keys
and release them before exiting.

The first bridge's endpoint applies to every connected script. Stop it before
changing `GRPC_REMOTE_ENDPOINT` (default `http://127.0.0.1:9036`). CLI usage:

```powershell
& "$env:ProgramFiles\GRPCRemote\RemoteInputBridge.exe" ping
& "$env:ProgramFiles\GRPCRemote\RemoteInputBridge.exe" press L --endpoint http://127.0.0.1:9036
RemoteInputBridge listen [--pipe NAME] [--endpoint URL]
```

The bridge targets .NET 8, links the server's key map and proto, and is published
as a self-contained executable by the installer. Mouse and hotkey commands remain
available through `GRPCRemoteClient`.

## Projects

| Project             | Type         | Description                                    |
|---------------------|--------------|------------------------------------------------|
| Keyboard Sender CLI | Console      | CLI for sending keyboard input via virtual HID |
| GRPCRemote          | ASP.NET Core | gRPC server for remote input injection         |
| GRPCRemoteClient    | Console      | CLI client for GRPCRemote                      |
| GRPCRemote.Tests    | xUnit        | Unit and integration tests                     |

## Requirements

- Windows 7, 8, 8.1 or 10 64-bit
- HVDK Standard drivers (available via ControlMyJoystick trial)
- .NET 8.0 SDK

## Building

```powershell
# Build all projects
just build

# Build in Release mode
just build-release

# Build GRPCRemote only
just build-grpcremote

# Build the Windows service installer MSI
dotnet build "GRPCRemoteInstaller\GRPCRemoteInstaller.wixproj" -c Release
```

## Testing

```powershell
# Run all tests
just test

# Run non-integration tests
just test-grpcremote

# Run integration tests
just test-grpcremote-integration
```

## GRPCRemote Usage

Start the server:
```powershell
dotnet run --project GRPCRemote/GRPCRemote.csproj
```

Run the published binary directly:

```powershell
.\GRPCRemote\bin\Release\net8.0\GRPCRemote.exe --urls http://0.0.0.0:9036
```

## Windows Service Installer

`GRPCRemoteInstaller` builds an MSI that installs a watchdog Windows Service
named `GRPCRemote`, which launches `GRPCRemote.exe` in the active interactive
console session.

- The service starts automatically at boot
- The service launches the worker with `--urls http://0.0.0.0:9036`
- The active console session owns the worker process
- Windows Service recovery is configured to restart it if it exits unexpectedly
- Windows Firewall rules are created for inbound and outbound TCP/UDP traffic for the installed service executable and port `9036`
- Roaming config is stored under `%AppData%\GRPCRemote\grpc-remote.config.json`
- Local logs are stored under `%LocalAppData%\GRPCRemote\logs\`
- Recording-mode output defaults to `%LocalAppData%\GRPCRemote\grpc-remote.events.jsonl`

Install silently:

```powershell
msiexec /i "GRPCRemoteInstaller\bin\Release\GRPCRemoteInstaller.msi" /quiet
```

Use the client:
```powershell
# Type text
GRPCRemoteClient/bin/Release/GRPCRemoteClient.exe type "Hello World"

# Press a key
GRPCRemoteClient/bin/Release/GRPCRemoteClient.exe key "a"

# Mouse click
GRPCRemoteClient/bin/Release/GRPCRemoteClient.exe click left
```

## GitHub Actions

The `release.yml` workflow builds and releases on tags (`v*`).
