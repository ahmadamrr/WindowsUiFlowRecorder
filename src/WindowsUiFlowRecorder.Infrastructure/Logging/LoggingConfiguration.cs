namespace WindowsUiFlowRecorder.Infrastructure.Logging;

using Microsoft.Extensions.Logging;

public static class LoggingConfiguration
{
    public static ILoggingBuilder ConfigureLocalLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss.fff ";
        });
        builder.AddProvider(new FileLoggerProvider(LogLevel.Information));
        builder.SetMinimumLevel(LogLevel.Information);
        return builder;
    }

    public static string GetLogsDirectory()
    {
        var provider = new FileLoggerProvider();
        return provider.LogsDirectory;
    }
}