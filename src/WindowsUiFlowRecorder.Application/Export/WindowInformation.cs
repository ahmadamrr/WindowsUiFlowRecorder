namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;

public record WindowInformation(
    Guid WindowId,
    string ApplicationTag,
    int ProcessId,
    string Title,
    string ClassName,
    BoundingRectangle BoundingRectangle,
    DateTime FirstCapturedAtUtc,
    DateTime LastUpdatedAtUtc,
    int CaptureCount,
    ElementInformation RootElement
);