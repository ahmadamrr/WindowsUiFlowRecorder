namespace WindowsUiFlowRecorder.Domain.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IApplicationProfileRepository
{
    Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync();
    Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId);
    Task<Result> SaveProfileAsync(ApplicationProfile profile);
    Task<Result> DeleteProfileAsync(Guid profileId);
}