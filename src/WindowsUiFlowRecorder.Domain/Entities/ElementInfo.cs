namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public record ElementInfo(
    string ElementId,
    string? AutomationId,
    string? Name,
    string ControlType,
    string? LocalizedControlType,
    string? ClassName,
    string? HelpText,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsKeyboardFocusable,
    BoundingRectangle BoundingRectangle,
    IReadOnlyList<string> SupportedPatterns,
    string? ValueOrText,
    int DepthInTree,
    int ProcessId,
    IReadOnlyList<ElementInfo> Children
);