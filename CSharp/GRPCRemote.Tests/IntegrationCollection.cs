namespace GRPCRemote.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<GrpcRemoteServerFixture>
{
    public const string Name = "GRPCRemote integration";
}
