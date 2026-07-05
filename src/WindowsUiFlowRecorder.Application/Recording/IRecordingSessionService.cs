namespace WindowsUiFlowRecorder.Application.Recording;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IRecordingSessionService
{
    Task<Result> StartSessionAsync(
        ApplicationLaunchChain launchChain,
        IReadOnlyList<TargetApplicationContext> contexts,
        CancellationToken ct);

    Result PauseSession();
    Result ResumeSession();
    Task<Result<RecordingSession>> StopSessionAsync();

    RecordingSessionState CurrentState { get; }
    event Action<RecordingSessionState>? StateChanged;
    SessionListItem? GetSessionSummary();
}