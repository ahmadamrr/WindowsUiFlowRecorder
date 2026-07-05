namespace WindowsUiFlowRecorder.Application.Scanning;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class UiScanService : IUiScanService
{
    private readonly IUiAutomationProvider _automation;
    private readonly ILogger<UiScanService> _logger;

    public UiScanService(IUiAutomationProvider automation, ILogger<UiScanService> logger)
    {
        _automation = automation;
        _logger = logger;
    }

    public async Task<Result<WindowSnapshot>> ScanWindowAsync(
        IntPtr windowHandle, int maxElementCount, CancellationToken ct)
    {
        _logger.LogInformation("Scanning window handle {Handle}", windowHandle);
        return await _automation.WalkHierarchyAsync(windowHandle, maxElementCount, ct);
    }
}