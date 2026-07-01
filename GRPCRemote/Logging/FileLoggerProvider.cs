using System.Collections.Concurrent;

namespace GRPCRemote.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _logDirectory;
    private readonly string _filePrefix;
    private readonly object _writeSync = new();
    private bool _disposed;

    public FileLoggerProvider(string logDirectory, string filePrefix = "grpc-remote")
    {
        _logDirectory = logDirectory;
        _filePrefix = filePrefix;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _loggers.GetOrAdd(categoryName, category => new FileLogger(category, WriteLine));
    }

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private string GetCurrentLogFilePath()
    {
        var fileName = $"{_filePrefix}-{DateTime.UtcNow:yyyy-MM-dd}.log";
        return Path.Combine(_logDirectory, fileName);
    }

    private void WriteLine(string message)
    {
        lock (_writeSync)
        {
            Directory.CreateDirectory(_logDirectory);
            File.AppendAllText(GetCurrentLogFilePath(), message + Environment.NewLine);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly IDisposable NullScope = new NoopScope();

        private readonly string _categoryName;
        private readonly Action<string> _writeLine;

        public FileLogger(string categoryName, Action<string> writeLine)
        {
            _categoryName = categoryName;
            _writeLine = writeLine;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {_categoryName}: {message}";
            if (exception is not null)
            {
                line = line + Environment.NewLine + exception;
            }

            _writeLine(line);
        }

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
