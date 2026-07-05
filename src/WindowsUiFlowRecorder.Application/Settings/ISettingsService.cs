namespace WindowsUiFlowRecorder.Application.Settings;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface ISettingsService
{
    Task<Result<Settings>> GetSettingsAsync();
    Task<Result> UpdateSettingsAsync(Settings settings);
}