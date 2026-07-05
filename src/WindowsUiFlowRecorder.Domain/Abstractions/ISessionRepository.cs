namespace WindowsUiFlowRecorder.Domain.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface ISessionRepository
{
    Task<Result> SaveSessionAsync(RecordingSession session);
    Task<Result<RecordingSession>> LoadSessionAsync(Guid sessionId);
    Task<Result<IReadOnlyList<SessionListItem>>> ListSessionsAsync();
    Task<Result> DeleteSessionAsync(Guid sessionId);
    Task<Result> UpdateSessionMetadataAsync(Guid sessionId, string? name, string? note);
}