# HVDK (Tetherscript Virtual HID Driver Kit)

Windows SDK for sending keyboard, mouse, joystick, and gamepad input via Tetherscript's Virtual HID drivers.

## Projects

| Project | Type | Description |
|---------|------|-------------|
| Keyboard Sender | WinForms | Sends keyboard input via virtual HID |
| Keyboard Reader | WinForms | Reads keyboard input from virtual HID |
| Mouse Sender Abs | WinForms | Sends absolute mouse position |
| Mouse Sender Rel | WinForms | Sends relative mouse movement |
| JoystickSenderAndReader | WPF | Sends and reads joystick/gamepad input |
| GRPCRemote | ASP.NET Core | gRPC server for remote input injection |
| GRPCRemoteClient | Console | CLI client for GRPCRemote |
| GRPCRemote.Tests | xUnit | Unit and integration tests |

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
