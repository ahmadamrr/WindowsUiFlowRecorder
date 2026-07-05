namespace WindowsUiFlowRecorder.Application.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Policies;

public interface IGlobalInputHook
{
    Task SubscribeAsync(Action<RawInputEvent> onInputEvent, CancellationToken ct);
    Task UnsubscribeAsync();
    DateTime LastHeartbeatUtc { get; }
}