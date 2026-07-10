namespace WindowsUiFlowRecorder.Infrastructure.Logging;

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logsDirectory;
    private readonly LogLevel _minLevel;
    private readonly object _lock = new();
    private string? _currentDate;
    private StreamWriter? _writer;

    private static readonly string[] LogLevelNames =
        ["TRACE", "DEBUG", "INFO ", "WARN ", "ERROR", "CRITICAL"];

    public FileLoggerProvider(LogLevel minLevel = LogLevel.Information)
    {
        var localAppData = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        _logsDirectory = Path.Combine(localAppData, "WindowsUiFlowRecorder", "Logs");
        _minLevel = minLevel;
        Directory.CreateDirectory(_logsDirectory);
    }

    internal string LogsDirectory => _logsDirectory;

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, () => this, _minLevel);

    public void WriteEntry(LogLevel logLevel, string category, string message, Exception? exception)
    {
        var now = DateTime.UtcNow;
        var dateKey = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var timestamp = now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var levelName = GetLevelName(logLevel);

        lock (_lock)
        {
            if (_currentDate != dateKey)
            {
                _writer?.Dispose();
                _writer = null;
                _currentDate = dateKey;
            }

            _writer ??= CreateWriter(dateKey);

            _writer.WriteLine($"{timestamp} [{levelName}] {category}: {message}");

            if (exception != null)
            {
                _writer.WriteLine($"{timestamp} [{levelName}] {category}: --- Exception ---");
                _writer.WriteLine($"{timestamp} [{levelName}] {category}: Type: {exception.GetType().FullName}");
                _writer.WriteLine($"{timestamp} [{levelName}] {category}: Message: {exception.Message}");
                _writer.WriteLine($"{timestamp} [{levelName}] {category}: StackTrace:");
                _writer.WriteLine(IndentStackTrace(exception.StackTrace));
                if (exception.InnerException != null)
                {
                    _writer.WriteLine($"{timestamp} [{levelName}] {category}: --- Inner Exception ---");
                    _writer.WriteLine($"{timestamp} [{levelName}] {category}: Type: {exception.InnerException.GetType().FullName}");
                    _writer.WriteLine($"{timestamp} [{levelName}] {category}: Message: {exception.InnerException.Message}");
                    _writer.WriteLine($"{timestamp} [{levelName}] {category}: StackTrace:");
                    _writer.WriteLine(IndentStackTrace(exception.InnerException.StackTrace));
                }
            }

            _writer.Flush();
        }
    }

    private StreamWriter CreateWriter(string dateKey)
    {
        var filePath = Path.Combine(_logsDirectory, $"{dateKey}.log");
        return new StreamWriter(filePath, append: true)
        {
            AutoFlush = false
        };
    }

    private static string GetLevelName(LogLevel level)
    {
        var index = (int)level;
        return index >= 0 && index < LogLevelNames.Length
            ? LogLevelNames[index]
            : "?????";
    }

    private static string IndentStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return "       (no stack trace available)";

        return "       " + stackTrace.Replace(Environment.NewLine, Environment.NewLine + "       ");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
