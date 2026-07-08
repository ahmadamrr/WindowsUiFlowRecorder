namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record Settings(
    ScreenshotMode ScreenshotMode,
    bool CaptureElementCroppedScreenshot,
    HierarchyRecaptureSensitivity HierarchyRecaptureSensitivity,
    string? DefaultExportDirectory,
    int DefaultReadinessConditionTimeoutSeconds,
    int DefaultReadinessPollIntervalMilliseconds,
    int MaxHierarchyElementCount,
    HierarchyExportScope HierarchyExportScope,
    bool VerboseDiagnosticLoggingEnabled,
    DateTime LastModifiedAtUtc
);