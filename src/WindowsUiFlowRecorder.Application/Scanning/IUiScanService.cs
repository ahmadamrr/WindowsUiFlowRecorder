namespace WindowsUiFlowRecorder.Application.Scanning;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IUiScanService
{
    Task<Result<WindowSnapshot>> ScanWindowAsync(IntPtr windowHandle, int maxElementCount, CancellationToken ct);
}