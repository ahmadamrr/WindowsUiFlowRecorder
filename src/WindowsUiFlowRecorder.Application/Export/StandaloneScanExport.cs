namespace WindowsUiFlowRecorder.Application.Export;

public record StandaloneScanExport(
    Guid ScanId,
    DateTime ScannedAtUtc,
    TargetApplicationInformation TargetApplication,
    IReadOnlyList<WindowInformation> Windows
);