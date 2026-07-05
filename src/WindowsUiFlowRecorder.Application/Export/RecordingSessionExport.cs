namespace WindowsUiFlowRecorder.Application.Export;

public record RecordingSessionExport(
    Guid SessionId,
    string Name,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime StartedAtUtc,
    DateTime StoppedAtUtc,
    int DurationSeconds,
    string TerminationReason,
    LaunchChainInformation? LaunchChain,
    IReadOnlyList<TargetApplicationInformation> TargetApplications,
    IReadOnlyList<RecordedActionExport> Actions,
    IReadOnlyList<WindowInformation> Windows,
    IReadOnlyList<ScreenshotInformation> Screenshots
);