namespace WindowsUiFlowRecorder.Application.Export;

public record TargetApplicationInformation(
    string ApplicationTag,
    string ExecutablePath,
    int ProcessId,
    int LaunchOrder,
    DateTime LaunchedAtUtc,
    DateTime? TerminatedAtUtc,
    string TerminationReason
);