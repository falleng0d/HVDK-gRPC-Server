# Build all projects
build:
    dotnet build Utils.sln

# Build GRPCRemote in Release mode
build-grpcremote:
    dotnet build GRPCRemote/GRPCRemote.csproj -c Release

# Build the GRPCRemote watchdog service in Release mode
build-grpcremote-service:
    dotnet build GRPCRemoteService/GRPCRemoteService.csproj -c Release

# Build the Windows service installer MSI
build-installer:
    dotnet build GRPCRemoteInstaller/GRPCRemoteInstaller.wixproj -c Release

# Build in Release mode
build-release:
    dotnet build Utils.sln -c Release

# Build a specific project
build-project PROJECT:
    dotnet build {{PROJECT}}

# Run all tests
test:
    dotnet test

# Run non-integration tests for GRPCRemote
test-grpcremote:
    dotnet test GRPCRemote.Tests/GRPCRemote.Tests.csproj --filter "FullyQualifiedName!~Integration"

# Run integration tests for GRPCRemote
test-grpcremote-integration:
    dotnet test GRPCRemote.Tests/GRPCRemote.Tests.csproj --filter "FullyQualifiedName~Integration"

# Clean build artifacts
clean:
    dotnet clean Utils.sln

# Restore NuGet packages
restore:
    dotnet restore Utils.sln
