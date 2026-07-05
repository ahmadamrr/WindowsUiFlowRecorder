namespace WindowsUiFlowRecorder.Application.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;

public interface IProcessLaunchMonitor
{
    Task<Result<int>> StartProcessAsync(string executablePath, string? arguments, string? workingDirectory, CancellationToken ct);
    Task<bool> IsProcessRunningAsync(int processId);
    Task<Result<IReadOnlyList<IntPtr>>> EnumerateTopLevelWindowsAsync(int processId);
    Task SubscribeToExitEventsAsync(IReadOnlyList<int> processIds, Action<int> onProcessExited);
    Task UnsubscribeAllAsync();
    Task KillProcessAsync(int processId);
}