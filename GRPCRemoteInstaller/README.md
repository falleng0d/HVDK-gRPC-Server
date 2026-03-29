# GRPCRemote Installer

This project builds an MSI that installs `GRPCRemoteService.exe` as a Windows
Service and deploys `GRPCRemote.exe` as the user-session worker it supervises.

Build from the solution root:

```powershell
dotnet build "GRPCRemoteInstaller\GRPCRemoteInstaller.wixproj" -c Release
```

The installer build publishes both `GRPCRemoteService` and `GRPCRemote` as
self-contained `win-x64` apps, generates WiX authoring for the published
files, and produces an MSI.

Output:

```powershell
GRPCRemoteInstaller\bin\Release\GRPCRemoteInstaller.msi
```
