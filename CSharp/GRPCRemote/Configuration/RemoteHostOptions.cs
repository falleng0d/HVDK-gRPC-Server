namespace GRPCRemote.Configuration;

public sealed class RemoteHostOptions
{
    public const string SectionName = "GRPCRemote";

    public string DriverMode { get; set; } = "Real";

    public string ConfigPath { get; set; } = "grpc-remote.config.json";

    public string RecordingPath { get; set; } = "grpc-remote.events.jsonl";

    public int KeyboardReportTimeoutMs { get; set; } = 5000;
}
