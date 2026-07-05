namespace WindowsUiFlowRecorder.Application.Launching;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IApplicationLaunchOrchestrator
{
    Task<Result<IReadOnlyList<TargetApplicationContext>>> ExecuteLaunchChainAsync(
        ApplicationLaunchChain chain,
        int pollIntervalMs,
        CancellationToken ct);
}