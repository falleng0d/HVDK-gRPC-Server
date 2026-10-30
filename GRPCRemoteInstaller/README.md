# GRPCRemote Installer

This project builds an MSI that installs `GRPCRemoteService.exe` as a Windows
Service and deploys `GRPCRemote.exe` as the user-session worker it supervises.

Build from the solution root:

```powershell
dotnet build "GRPCRemoteInstaller\GRPCRemoteInstaller.wixproj" -c Release
```

The installer build publishes `GRPCRemoteService`, `GRPCRemote`, and `RemoteInputBridge` as
self-contained `win-x64` apps, generates WiX authoring for the published
files, and produces an MSI.

`RemoteInputBridge.exe` is published as a single-file app to avoid dependency
collisions with the service. It and `remote_input.ahk` are harvested into
`C:\Program Files\GRPCRemote` (or the selected `INSTALLFOLDER`). AutoHotkey v1.1.28+
64-bit scripts can use `#Include %A_ProgramFiles%\GRPCRemote\remote_input.ahk`.
See the main README for helpers, protocol, and shared bridge lifetime.

At runtime:

- config is stored under `%AppData%\GRPCRemote\`
- logs are stored under `%LocalAppData%\GRPCRemote\logs\`
- recording-mode event output is stored under `%LocalAppData%\GRPCRemote\`

Output:

```powershell
GRPCRemoteInstaller\bin\x64\Release\GRPCRemoteInstaller.msi
```
