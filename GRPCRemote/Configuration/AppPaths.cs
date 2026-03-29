namespace GRPCRemote.Configuration;

public static class AppPaths
{
    public const string AppFolderName = "GRPCRemote";

    public static string GetRoamingDataDirectory()
    {
        return EnsureDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
    }

    public static string GetLocalDataDirectory()
    {
        return EnsureDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
    }

    public static string GetLogsDirectory()
    {
        return EnsureDirectory(GetLocalDataDirectory(), "logs");
    }

    public static string ResolveConfigPath(string configuredPath)
    {
        return ResolvePath(configuredPath, GetRoamingDataDirectory(), "grpc-remote.config.json");
    }

    public static string ResolveRecordingPath(string configuredPath)
    {
        return ResolvePath(configuredPath, GetLocalDataDirectory(), "grpc-remote.events.jsonl");
    }

    private static string ResolvePath(string configuredPath, string defaultDirectory, string defaultFileName)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        if (string.IsNullOrWhiteSpace(configuredPath) ||
            string.Equals(configuredPath, defaultFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(defaultDirectory, defaultFileName);
        }

        return Path.GetFullPath(configuredPath);
    }

    private static string EnsureDirectory(params string[] parts)
    {
        var path = Path.Combine(parts);
        Directory.CreateDirectory(path);
        return path;
    }
}
