namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;

public record ElementInformation(
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
    IReadOnlyList<ElementInformation> Children
);