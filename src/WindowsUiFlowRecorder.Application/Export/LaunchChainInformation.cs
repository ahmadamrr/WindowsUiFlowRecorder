namespace WindowsUiFlowRecorder.Application.Export;

public record LaunchChainInformation(
    Guid? ApplicationProfileId,
    string? ApplicationProfileName,
    IReadOnlyList<LaunchStepInformation> Steps
);

public record LaunchStepInformation(
    int StepOrder,
    string ApplicationTag,
    string ExecutablePath,
    string? Arguments,
    ReadinessConditionInformation ReadinessCondition,
    int ReadinessTimeoutSeconds,
    int ActualWaitDurationSeconds
);

public record ReadinessConditionInformation(
    string ConditionType,
    string Summary
);