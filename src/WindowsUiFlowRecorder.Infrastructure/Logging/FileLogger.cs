namespace WindowsUiFlowRecorder.Infrastructure.Logging;

using Microsoft.Extensions.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Func<FileLoggerProvider> _providerFactory;
    private readonly LogLevel _minLevel;

    public FileLogger(string categoryName, Func<FileLoggerProvider> providerFactory, LogLevel minLevel)
    {
        _categoryName = categoryName;
        _providerFactory = providerFactory;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var provider = _providerFactory();
        provider.WriteEntry(logLevel, _categoryName, message, exception);
    }
}
