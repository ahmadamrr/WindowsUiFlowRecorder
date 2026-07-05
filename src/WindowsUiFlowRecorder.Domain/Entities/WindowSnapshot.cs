namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record WindowSnapshot(
    Guid WindowId,
    string ApplicationTag,
    int ProcessId,
    string Title,
    string ClassName,
    BoundingRectangle BoundingRectangle,
    DateTime FirstCapturedAtUtc,
    DateTime LastUpdatedAtUtc,
    int CaptureCount,
    ElementInfo RootElement,
    StructuralFingerprint StructuralFingerprint
);