# HVDK (Tetherscript Virtual HID Driver Kit)

The HVDK is a Windows SDK that allows you to send data to Tetherscript's Virtual HID
Keyboard and Mouse drivers. These are the same drivers used by
Tetherscript's ControlMyJoystick program.

## Requirements

- **OS**: Windows 7, 8, 8.1 or 10 64-bit (drivers are 64-bit only)
- **Drivers**: HVDK Standard drivers must be installed (available via the free trial of
  ControlMyJoystick at tetherscript.com)
- **.NET 8.0 SDK** (required to build)

## Projects

| Project          | Type          | Description                           |
|------------------|---------------|---------------------------------------|
| Keyboard Sender CLI | Console    | CLI for sending keyboard input via virtual HID |
| GRPCRemote       | ASP.NET Core | gRPC server for remote input injection |
| GRPCRemoteClient | Console      | CLI client for GRPCRemote             |
| GRPCRemote.Tests | xUnit        | Unit and integration tests           |

---

# GRPCRemote

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

---

## Building

Use dotnet CLI:

```powershell
dotnet build "GRPCRemote\GRPCRemote.csproj" -c Release
dotnet build "GRPCRemoteClient\GRPCRemoteClient.csproj" -c Release
```

Output binaries are placed in each project's `bin\Release\` folder.

---

## Testing

### Unit/Integration Tests

```powershell
dotnet test "GRPCRemote.Tests\GRPCRemote.Tests.csproj"
```

### Running Server in Background with PTY

To run the server interactively in a PTY session:

```powershell
# Start the GRPCRemote server in background
pty_spawn(
    command="dotnet",
    args=["run", "--project", "GRPCRemote/GRPCRemote.csproj"],
    workdir="CSharp",
    title="GRPCRemote Server",
    description="Start gRPC server in background"
)

# Wait for server to start, then send commands with the client
pty_write(id="pty_XXXXXXXX", data="...\n")

# Read server output
pty_read(id="pty_XXXXXXXX")
```

### Using GRPCRemoteClient CLI

After building, run the client to send commands to the server:

```powershell
# Type text
.\GRPCRemoteClient\bin\Release\GRPCRemoteClient.exe type "Hello World"

# Press a key
.\GRPCRemoteClient\bin\Release\GRPCRemoteClient.exe key "a"

# Mouse click
.\GRPCRemoteClient\bin\Release\GRPCRemoteClient.exe click left

# Get help
.\GRPCRemoteClient\bin\Release\GRPCRemoteClient.exe --help
```

To read server logs after running, use `pty_read` with the PTY session ID.

---

## IDE Tools

Use the `ide` CLI for diagnostics and linting:

`ide get-problems --path "./GRPCRemote/Input/KeyRequestOptions.cs"`

---

## Notes

- HVDK drivers must be installed for actual hardware injection (Use `wmic path Win32_PnPEntity where "Name like '%Tetherscript%' OR Name like '%Virtual Keyboard%' OR Name like '%Virtual HID%'" get Name,PNPDeviceID,Status`) to verify installation if issues are encountered.
- Without drivers, the server runs but cannot inject input
- The `RecordingInputTransport` is used in tests to verify events without hardware
- A delay was added in `InputCoordinator.cs` to prevent commands being sent too fast (causes commands to not be executed by the driver)

## Release Install

1. `gh release download $(gh release list --limit 1 --json tagName --jq '.[0].tagName') --pattern "*.msi" --dir "$env:TEMP\hvdk-release" --clobber`
2. `scp "$env:TEMP\hvdk-release\GRPCRemoteInstaller.msi" qud:"C:/Users/falleng0d/AppData/Local/Temp/GRPCRemoteInstaller.msi"`
3. `msiexec /i "$env:TEMP\hvdk-release\GRPCRemoteInstaller.msi" /qn /norestart /L*v "$env:TEMP\GRPCRemoteInstaller.log"; ssh qud 'msiexec /i "C:\Users\falleng0d\AppData\Local\Temp\GRPCRemoteInstaller.msi" /qn /norestart /L*v "C:\Users\falleng0d\AppData\Local\Temp\GRPCRemoteInstaller.log"'`
