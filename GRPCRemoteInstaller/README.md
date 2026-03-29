# GRPCRemote Installer

This project builds an MSI that installs `GRPCRemote.exe` as a Windows Service.

Build from the solution root:

```powershell
dotnet build "GRPCRemoteInstaller\GRPCRemoteInstaller.wixproj" -c Release
```

The installer build publishes `GRPCRemote` as a self-contained `win-x64` app,
generates WiX authoring for the published files, and produces an MSI.

Output:

```powershell
GRPCRemoteInstaller\bin\Release\GRPCRemoteInstaller.msi
```
