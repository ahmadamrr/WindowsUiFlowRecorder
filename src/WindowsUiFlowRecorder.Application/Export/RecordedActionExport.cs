namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;

public record RecordedActionExport(
    Guid ActionId,
    int SequenceNumber,
    DateTime TimestampUtc,
    string ActionType,
    string ApplicationTag,
    Guid WindowId,
    ElementInformation? TargetElement,
    IReadOnlyList<string> ElementPath,
    ScreenPoint? ScreenPoint,
    ScreenPoint? DragStartPoint,
    string? EnteredText,
    string? KeyName,
    Guid? PreviousWindowId,
    Guid? ScreenshotId
);