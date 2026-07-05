namespace WindowsUiFlowRecorder.Domain.Entities;

public record LaunchStep(
    int StepOrder,
    string ApplicationTag,
    string ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    ReadinessCondition ReadinessCondition,
    int? ReadinessTimeoutSecondsOverride,
    bool CleanUpOnFailure
);