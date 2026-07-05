namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record RecordedAction(
    Guid ActionId,
    int SequenceNumber,
    DateTime TimestampUtc,
    ActionType ActionType,
    string ApplicationTag,
    Guid WindowId,
    ElementInfo? TargetElement,
    IReadOnlyList<string> ElementPath,
    ScreenPoint? ScreenPoint,
    ScreenPoint? DragStartPoint,
    string? EnteredText,
    string? KeyName,
    Guid? PreviousWindowId,
    Guid? ScreenshotId
);