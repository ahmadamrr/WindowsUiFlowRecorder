namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record ScreenshotReference(
    Guid ScreenshotId,
    string RelativeFilePath,
    ScreenshotScope Scope,
    ScreenshotFormat Format,
    int Width,
    int Height,
    DateTime CapturedAtUtc,
    Guid? AssociatedActionId,
    Guid? AssociatedWindowId,
    string WorkingFilePath
);