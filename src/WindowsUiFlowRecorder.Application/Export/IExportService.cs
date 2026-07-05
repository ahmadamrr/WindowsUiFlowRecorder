namespace WindowsUiFlowRecorder.Application.Export;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IExportService
{
    Task<Result> ExportSessionAsync(RecordingSession session, string outputDirectory, CancellationToken ct);
    Task<Result> ExportStandaloneScanAsync(WindowSnapshot snapshot, string outputDirectory, CancellationToken ct);
}