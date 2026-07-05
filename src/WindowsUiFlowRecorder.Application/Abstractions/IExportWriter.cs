namespace WindowsUiFlowRecorder.Application.Abstractions;

using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IExportWriter
{
    Task<Result> WriteExportAsync(
        ExportPackage exportPackage,
        string outputDirectory,
        IReadOnlyList<ScreenshotReference> screenshots,
        CancellationToken ct);
}