namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;

public record ScreenshotInformation(
    Guid ScreenshotId,
    string RelativeFilePath,
    string Scope,
    string Format,
    int Width,
    int Height,
    DateTime CapturedAtUtc,
    Guid? AssociatedActionId,
    Guid? AssociatedWindowId
);