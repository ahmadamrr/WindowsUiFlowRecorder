namespace WindowsUiFlowRecorder.Domain.Entities;

public record SessionListItem(
    Guid SessionId,
    string Name,
    DateTime CreatedAtUtc,
    int DurationSeconds,
    IReadOnlyList<string> TargetApplicationTags,
    int ActionCount,
    int WindowCount,
    int ScreenshotCount,
    string? Note
);