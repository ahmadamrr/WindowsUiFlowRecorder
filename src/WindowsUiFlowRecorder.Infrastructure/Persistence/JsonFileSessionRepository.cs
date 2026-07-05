namespace WindowsUiFlowRecorder.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class JsonFileSessionRepository : ISessionRepository
{
    private readonly string _sessionsDir;
    private readonly ILogger<JsonFileSessionRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public JsonFileSessionRepository(ILogger<JsonFileSessionRepository> logger)
    {
        _logger = logger;
        _sessionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUiFlowRecorder", "Sessions");
        Directory.CreateDirectory(_sessionsDir);
    }

    public async Task<Result> SaveSessionAsync(RecordingSession session)
    {
        try
        {
            var dir = Path.Combine(_sessionsDir, session.SessionId.ToString());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "session.json");
            var json = JsonSerializer.Serialize(session, JsonOptions);
            await File.WriteAllTextAsync(path, json);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session {Id}", session.SessionId);
            return Result.Failure(FailureReason.DiskWriteFailed, ex.Message);
        }
    }

    public async Task<Result<RecordingSession>> LoadSessionAsync(Guid sessionId)
    {
        try
        {
            var path = Path.Combine(_sessionsDir, sessionId.ToString(), "session.json");
            if (!File.Exists(path))
                return Result<RecordingSession>.Failure(FailureReason.SessionNotFound, "Session file not found");

            var json = await File.ReadAllTextAsync(path);
            var session = JsonSerializer.Deserialize<RecordingSession>(json, JsonOptions);
            return session != null
                ? Result<RecordingSession>.Success(session)
                : Result<RecordingSession>.Failure(FailureReason.SerializationFailed, "Failed to deserialize session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {Id}", sessionId);
            return Result<RecordingSession>.Failure(FailureReason.SerializationFailed, ex.Message);
        }
    }

    public Task<Result<IReadOnlyList<SessionListItem>>> ListSessionsAsync()
    {
        try
        {
            var items = new List<SessionListItem>();
            if (!Directory.Exists(_sessionsDir))
                return Task.FromResult(Result<IReadOnlyList<SessionListItem>>.Success(items.AsReadOnly()));

            foreach (var dir in Directory.GetDirectories(_sessionsDir))
            {
                var sessionFile = Path.Combine(dir, "session.json");
                if (!File.Exists(sessionFile)) continue;

                var json = File.ReadAllText(sessionFile);
                var session = JsonSerializer.Deserialize<RecordingSession>(json, JsonOptions);
                if (session == null) continue;

                items.Add(new SessionListItem(
                    session.SessionId, session.Name, session.CreatedAtUtc,
                    (int)((session.StoppedAtUtc ?? DateTime.UtcNow) - (session.StartedAtUtc ?? DateTime.UtcNow)).TotalSeconds,
                    session.TargetApplicationContexts.Select(c => c.ApplicationTag).ToList(),
                    session.Actions.Count, session.Windows.Count, 0, session.Note));
            }

            return Task.FromResult(Result<IReadOnlyList<SessionListItem>>.Success(items.AsReadOnly()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list sessions");
            return Task.FromResult(Result<IReadOnlyList<SessionListItem>>.Failure(
                FailureReason.SerializationFailed, ex.Message));
        }
    }

    public Task<Result> DeleteSessionAsync(Guid sessionId)
    {
        try
        {
            var dir = Path.Combine(_sessionsDir, sessionId.ToString());
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {Id}", sessionId);
            return Task.FromResult(Result.Failure(FailureReason.DiskWriteFailed, ex.Message));
        }
    }

    public async Task<Result> UpdateSessionMetadataAsync(Guid sessionId, string? name, string? note)
    {
        var loadResult = await LoadSessionAsync(sessionId);
        if (!loadResult.IsSuccess)
            return Result.Failure(loadResult.FailureReason!.Value, loadResult.ErrorMessage);

        var session = loadResult.Value!;
        if (name != null) session.Name = name;
        if (note != null) session.Note = note;

        return await SaveSessionAsync(session);
    }
}