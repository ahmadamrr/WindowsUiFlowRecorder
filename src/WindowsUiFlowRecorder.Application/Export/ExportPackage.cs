namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;

public record ExportPackage(
    string SchemaVersion,
    string ToolVersion,
    DateTime ExportedAtUtc,
    ExportKind ExportKind,
    RecordingSessionExport? RecordingSession,
    StandaloneScanExport? StandaloneScan
);