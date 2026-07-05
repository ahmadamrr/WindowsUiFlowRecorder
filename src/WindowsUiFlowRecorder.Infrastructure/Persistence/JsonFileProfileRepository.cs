namespace WindowsUiFlowRecorder.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class JsonFileProfileRepository : IApplicationProfileRepository
{
    private readonly string _profilesDir;
    private readonly ILogger<JsonFileProfileRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public JsonFileProfileRepository(ILogger<JsonFileProfileRepository> logger)
    {
        _logger = logger;
        _profilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUiFlowRecorder", "Profiles");
        Directory.CreateDirectory(_profilesDir);
    }

    public Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync()
    {
        try
        {
            var profiles = new List<ApplicationProfile>();
            if (!Directory.Exists(_profilesDir))
                return Task.FromResult(Result<IReadOnlyList<ApplicationProfile>>.Success(profiles.AsReadOnly()));

            foreach (var file in Directory.GetFiles(_profilesDir, "*.json"))
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<ApplicationProfile>(json, JsonOptions);
                if (profile != null) profiles.Add(profile);
            }

            return Task.FromResult(Result<IReadOnlyList<ApplicationProfile>>.Success(profiles.AsReadOnly()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list profiles");
            return Task.FromResult(Result<IReadOnlyList<ApplicationProfile>>.Failure(
                FailureReason.SerializationFailed, ex.Message));
        }
    }

    public Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId)
    {
        try
        {
            var path = Path.Combine(_profilesDir, $"{profileId}.json");
            if (!File.Exists(path))
                return Task.FromResult(Result<ApplicationProfile>.Failure(
                    FailureReason.InvalidProfile, "Profile not found"));

            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<ApplicationProfile>(json, JsonOptions);
            return Task.FromResult(profile != null
                ? Result<ApplicationProfile>.Success(profile)
                : Result<ApplicationProfile>.Failure(FailureReason.SerializationFailed, "Failed to deserialize profile"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile {Id}", profileId);
            return Task.FromResult(Result<ApplicationProfile>.Failure(
                FailureReason.SerializationFailed, ex.Message));
        }
    }

    public async Task<Result> SaveProfileAsync(ApplicationProfile profile)
    {
        try
        {
            Directory.CreateDirectory(_profilesDir);
            var path = Path.Combine(_profilesDir, $"{profile.ProfileId}.json");
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await File.WriteAllTextAsync(path, json);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profile {Id}", profile.ProfileId);
            return Result.Failure(FailureReason.DiskWriteFailed, ex.Message);
        }
    }

    public Task<Result> DeleteProfileAsync(Guid profileId)
    {
        try
        {
            var path = Path.Combine(_profilesDir, $"{profileId}.json");
            if (File.Exists(path)) File.Delete(path);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete profile {Id}", profileId);
            return Task.FromResult(Result.Failure(FailureReason.DiskWriteFailed, ex.Message));
        }
    }
}