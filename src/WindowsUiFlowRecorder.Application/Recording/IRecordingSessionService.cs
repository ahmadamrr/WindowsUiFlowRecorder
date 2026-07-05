namespace WindowsUiFlowRecorder.Application.Recording;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IRecordingSessionService
{
    RecordingSessionState CurrentState { get; }
    RecordingSession? CurrentSession { get; }
    event Action<RecordingSessionState> StateChanged;
    event Action<string> ErrorOccurred;

    Task<Result> PrepareAsync(ApplicationLaunchChain launchChain, CancellationToken ct);
    Task<Result> StartRecordingAsync(CancellationToken ct);
    Result PauseSession();
    Result ResumeSession();
    Task<Result<RecordingSession>> StopSessionAsync(CancellationToken ct);
    void ResetToIdle();

    SessionListItem? GetSessionSummary();
    Task<Result> ExportSessionAsync(string outputDirectory, CancellationToken ct);
}