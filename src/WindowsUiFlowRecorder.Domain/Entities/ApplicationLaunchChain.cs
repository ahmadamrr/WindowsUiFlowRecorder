namespace WindowsUiFlowRecorder.Domain.Entities;

public record ApplicationLaunchChain(
    IReadOnlyList<LaunchStep> Steps
);