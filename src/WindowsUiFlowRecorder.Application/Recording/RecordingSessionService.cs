namespace WindowsUiFlowRecorder.Application.Recording;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class RecordingSessionService : IRecordingSessionService
{
    private readonly ILogger<RecordingSessionService> _logger;
    private RecordingSession? _session;
    private readonly object _lock = new();

    public RecordingSessionState CurrentState { get; private set; } = RecordingSessionState.Idle;
    public event Action<RecordingSessionState>? StateChanged;

    public RecordingSessionService(ILogger<RecordingSessionService> logger)
    {
        _logger = logger;
    }

    public Task<Result> StartSessionAsync(
        ApplicationLaunchChain launchChain,
        IReadOnlyList<TargetApplicationContext> contexts,
        CancellationToken ct)
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Configuring && CurrentState != RecordingSessionState.Idle)
                return Task.FromResult(Result.Failure(FailureReason.Unknown, "Session must be in Idle or Configuring state to start"));

            _session = new RecordingSession
            {
                SessionId = Guid.NewGuid(),
                Name = $"Session_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                State = RecordingSessionState.Recording,
                TargetApplicationContexts = [.. contexts],
                CreatedAtUtc = DateTime.UtcNow,
                StartedAtUtc = DateTime.UtcNow,
                ApplicationProfileId = null
            };

            SetState(RecordingSessionState.Recording);
            _logger.LogInformation("Session {SessionId} started with {ContextCount} contexts",
                _session.SessionId, contexts.Count);
            return Task.FromResult(Result.Success());
        }
    }

    public Result PauseSession()
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Recording)
                return Result.Failure(FailureReason.Unknown, "Session is not recording");

            SetState(RecordingSessionState.Paused);
            _logger.LogInformation("Session paused");
            return Result.Success();
        }
    }

    public Result ResumeSession()
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Paused)
                return Result.Failure(FailureReason.Unknown, "Session is not paused");

            SetState(RecordingSessionState.Recording);
            _logger.LogInformation("Session resumed");
            return Result.Success();
        }
    }

    public Task<Result<RecordingSession>> StopSessionAsync()
    {
        lock (_lock)
        {
            if (CurrentState is not (RecordingSessionState.Recording or RecordingSessionState.Paused))
                return Task.FromResult(Result<RecordingSession>.Failure(FailureReason.Unknown, "No active session to stop"));

            if (_session == null)
                return Task.FromResult(Result<RecordingSession>.Failure(FailureReason.SessionNotFound, "No session exists"));

            _session.StoppedAtUtc = DateTime.UtcNow;
            _session.State = RecordingSessionState.Stopped;

            SetState(RecordingSessionState.Stopped);
            _logger.LogInformation("Session {SessionId} stopped with {ActionCount} actions",
                _session.SessionId, _session.Actions.Count);

            return Task.FromResult(Result<RecordingSession>.Success(_session));
        }
    }

    public SessionListItem? GetSessionSummary()
    {
        lock (_lock)
        {
            if (_session == null) return null;

            return new SessionListItem(
                _session.SessionId,
                _session.Name,
                _session.CreatedAtUtc,
                (int)((_session.StoppedAtUtc ?? DateTime.UtcNow) - _session.StartedAtUtc ?? TimeSpan.Zero).TotalSeconds,
                _session.TargetApplicationContexts.Select(c => c.ApplicationTag).ToList(),
                _session.Actions.Count,
                _session.Windows.Count,
                0,
                _session.Note
            );
        }
    }

    private void SetState(RecordingSessionState newState)
    {
        CurrentState = newState;
        StateChanged?.Invoke(newState);
    }
}