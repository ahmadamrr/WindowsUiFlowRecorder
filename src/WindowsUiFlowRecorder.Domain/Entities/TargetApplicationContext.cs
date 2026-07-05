namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record TargetApplicationContext(
    string ApplicationTag,
    string ExecutablePath,
    int ProcessId,
    int LaunchOrder,
    DateTime LaunchedAtUtc,
    bool IsActive,
    DateTime? TerminatedAtUtc,
    TargetTerminationReason TerminationReason
);