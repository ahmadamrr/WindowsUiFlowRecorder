namespace WindowsUiFlowRecorder.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class JsonFileSettingsRepository : ISettingsRepository
{
    private readonly string _settingsPath;
    private readonly ILogger<JsonFileSettingsRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public JsonFileSettingsRepository(ILogger<JsonFileSettingsRepository> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUiFlowRecorder", "Settings");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public Task<Result<Settings>> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new Settings(
                    ScreenshotMode.EveryAction, false,
                    HierarchyRecaptureSensitivity.Medium, null,
                    30, 250, 5000, HierarchyExportScope.FullTree,
                    false, DateTime.UtcNow);
                return Task.FromResult(Result<Settings>.Success(defaults));
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
            return Task.FromResult(settings != null
                ? Result<Settings>.Success(settings)
                : Result<Settings>.Failure(FailureReason.SerializationFailed, "Failed to deserialize settings"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            return Task.FromResult(Result<Settings>.Failure(
                FailureReason.SerializationFailed, ex.Message));
        }
    }

    public async Task<Result> SaveSettingsAsync(Settings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            return Result.Failure(FailureReason.DiskWriteFailed, ex.Message);
        }
    }
}