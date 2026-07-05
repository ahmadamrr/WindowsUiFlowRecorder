namespace WindowsUiFlowRecorder.Application.Profiles;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IApplicationProfileService
{
    Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync();
    Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId);
    Task<Result> SaveProfileAsync(ApplicationProfile profile);
    Task<Result> DeleteProfileAsync(Guid profileId);
    Task<Result<ApplicationProfile>> DuplicateProfileAsync(Guid profileId, string newName);
}