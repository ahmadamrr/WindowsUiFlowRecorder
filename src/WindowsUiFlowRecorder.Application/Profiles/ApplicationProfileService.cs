namespace WindowsUiFlowRecorder.Application.Profiles;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ApplicationProfileService : IApplicationProfileService
{
    private readonly IApplicationProfileRepository _repository;
    private readonly ILogger<ApplicationProfileService> _logger;

    public ApplicationProfileService(
        IApplicationProfileRepository repository,
        ILogger<ApplicationProfileService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync()
        => await _repository.GetAllProfilesAsync();

    public async Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId)
        => await _repository.GetProfileAsync(profileId);

    public async Task<Result> SaveProfileAsync(ApplicationProfile profile)
    {
        var updated = profile with
        {
            LastModifiedAtUtc = DateTime.UtcNow
        };
        return await _repository.SaveProfileAsync(updated);
    }

    public async Task<Result> DeleteProfileAsync(Guid profileId)
        => await _repository.DeleteProfileAsync(profileId);

    public async Task<Result<ApplicationProfile>> DuplicateProfileAsync(Guid profileId, string newName)
    {
        var existing = await _repository.GetProfileAsync(profileId);
        if (!existing.IsSuccess)
            return Result<ApplicationProfile>.Failure(existing.FailureReason!.Value, existing.ErrorMessage);

        var duplicate = existing.Value! with
        {
            ProfileId = Guid.NewGuid(),
            Name = newName,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow
        };

        var saveResult = await _repository.SaveProfileAsync(duplicate);
        return saveResult.IsSuccess
            ? Result<ApplicationProfile>.Success(duplicate)
            : Result<ApplicationProfile>.Failure(saveResult.FailureReason!.Value, saveResult.ErrorMessage);
    }
}