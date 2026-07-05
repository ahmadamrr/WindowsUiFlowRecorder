namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record ReadinessCondition(
    ConditionType ConditionType,
    string? WindowTitlePattern,
    WindowMatchMode? WindowMatchMode,
    string? ElementAutomationId,
    string? ElementName,
    string? ElementControlType,
    ExpectedPropertyName? ExpectedPropertyName,
    string? ExpectedPropertyValue,
    PropertyMatchMode? PropertyMatchMode,
    int? FixedTimeoutSeconds
);