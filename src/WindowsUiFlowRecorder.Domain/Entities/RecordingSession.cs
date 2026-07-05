namespace WindowsUiFlowRecorder.Domain.Entities;

using WindowsUiFlowRecorder.Domain.Common;

public class RecordingSession
{
    public Guid SessionId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public RecordingSessionState State { get; set; } = RecordingSessionState.Idle;
    public Guid? ApplicationProfileId { get; init; }
    public List<TargetApplicationContext> TargetApplicationContexts { get; init; } = [];
    public List<RecordedAction> Actions { get; init; } = [];
    public Dictionary<Guid, WindowSnapshot> Windows { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? StoppedAtUtc { get; set; }
}