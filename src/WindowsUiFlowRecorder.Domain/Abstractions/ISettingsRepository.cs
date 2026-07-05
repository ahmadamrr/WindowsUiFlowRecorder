namespace WindowsUiFlowRecorder.Domain.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface ISettingsRepository
{
    Task<Result<Settings>> LoadSettingsAsync();
    Task<Result> SaveSettingsAsync(Settings settings);
}