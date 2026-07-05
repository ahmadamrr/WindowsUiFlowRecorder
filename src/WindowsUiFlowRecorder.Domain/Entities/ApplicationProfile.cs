namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record ApplicationProfile(
    Guid ProfileId,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    ApplicationLaunchChain LaunchChain
);