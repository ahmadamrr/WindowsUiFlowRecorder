namespace WindowsUiFlowRecorder.Application.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IUiAutomationProvider
{
    Task<Result<ElementInfo>> GetElementAtPointAsync(ScreenPoint point, CancellationToken ct);
    Task<Result<ElementInfo>> GetFocusedElementAsync(CancellationToken ct);
    Task<Result<WindowSnapshot>> GetWindowAtPointAsync(ScreenPoint point, int maxElementCount, CancellationToken ct);
    Task<Result<WindowSnapshot>> WalkHierarchyAsync(IntPtr windowHandle, int maxElementCount, CancellationToken ct);
    Task<Result<WindowSnapshot>> WalkHierarchyWithPidAsync(IntPtr windowHandle, int processId, int maxElementCount, CancellationToken ct);
    Task<Result> SubscribeToEventsAsync(
        IReadOnlyList<TargetApplicationContext> contexts,
        Action<ElementInfo> onFocusChanged,
        Action<WindowSnapshot> onWindowActivated,
        Action<IntPtr> onWindowOpened,
        CancellationToken ct);
    Task UnsubscribeAllAsync();
    Task<Result<WindowSnapshot>> GetOwningWindowAsync(ElementInfo element, CancellationToken ct);
}