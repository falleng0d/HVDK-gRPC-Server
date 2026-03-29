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
