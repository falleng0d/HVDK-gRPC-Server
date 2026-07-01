using System.Text.Json;
using GRPCRemote.Configuration;

namespace GRPCRemote.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly ILogger<ConfigService> _logger;
    private readonly string _configPath;
    private RuntimeConfig _config;

    public ConfigService(RemoteHostOptions options, ILogger<ConfigService> logger)
    {
        _logger = logger;
        _configPath = AppPaths.ResolveConfigPath(options.ConfigPath);

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _config = Load();
        Save();
    }

    public RuntimeConfig Snapshot
    {
        get
        {
            lock (_sync)
            {
                return Clone(_config);
            }
        }
    }

    public RuntimeConfig Update(float? cursorSpeed, float? cursorAcceleration)
    {
        lock (_sync)
        {
            if (cursorSpeed.HasValue)
            {
                ValidateRange(cursorSpeed.Value, 0f, 2f, nameof(cursorSpeed));
                _config.CursorSpeed = cursorSpeed.Value;
            }

            if (cursorAcceleration.HasValue)
            {
                ValidateRange(cursorAcceleration.Value, 0f, 2f, nameof(cursorAcceleration));
                _config.CursorAcceleration = cursorAcceleration.Value;
            }

            Save();
            return Clone(_config);
        }
    }

    private RuntimeConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Creating config file at {Path}", _configPath);
            return new RuntimeConfig();
        }

        var json = File.ReadAllText(_configPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RuntimeConfig();
        }

        return JsonSerializer.Deserialize<RuntimeConfig>(json, JsonOptions) ?? new RuntimeConfig();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_config, JsonOptions);
        File.WriteAllText(_configPath, json);
        _logger.LogInformation("Saved runtime config to {Path}", _configPath);
    }

    private static RuntimeConfig Clone(RuntimeConfig config)
    {
        return new RuntimeConfig
        {
            CursorSpeed = config.CursorSpeed,
            CursorAcceleration = config.CursorAcceleration,
            KeyPressInterval = config.KeyPressInterval,
        };
    }

    private static void ValidateRange(float value, float min, float max, string name)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(name, $"Value must be between {min} and {max}.");
        }
    }
}
