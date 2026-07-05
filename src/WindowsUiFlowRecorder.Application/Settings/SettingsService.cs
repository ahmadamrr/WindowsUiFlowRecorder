namespace WindowsUiFlowRecorder.Application.Settings;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ISettingsRepository repository, ILogger<SettingsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<Settings>> GetSettingsAsync()
        => await _repository.LoadSettingsAsync();

    public async Task<Result> UpdateSettingsAsync(Settings settings)
    {
        var updated = settings with { LastModifiedAtUtc = DateTime.UtcNow };
        return await _repository.SaveSettingsAsync(updated);
    }
}