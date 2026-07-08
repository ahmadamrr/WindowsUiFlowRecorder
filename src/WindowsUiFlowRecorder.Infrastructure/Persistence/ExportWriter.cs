namespace WindowsUiFlowRecorder.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ExportWriter : IExportWriter
{
    private readonly ILogger<ExportWriter> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ExportWriter(ILogger<ExportWriter> logger)
    {
        _logger = logger;
    }

    public async Task<Result> WriteExportAsync(
        ExportPackage exportPackage,
        string outputDirectory,
        IReadOnlyList<ScreenshotReference> screenshots,
        CancellationToken ct,
        string? exportFileName = null,
        string? screenshotsSubfolder = null)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);

            if (string.IsNullOrEmpty(exportFileName))
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                if (exportPackage.ExportKind == ExportKind.RecordingSession
                    && exportPackage.RecordingSession != null)
                {
                    var sessionName = SanitizeFileName(exportPackage.RecordingSession.Name);
                    exportFileName = $"recording_{sessionName}_{timestamp}.json";
                }
                else
                {
                    exportFileName = $"scan_{timestamp}.json";
                }
            }

            if (string.IsNullOrEmpty(screenshotsSubfolder))
                screenshotsSubfolder = exportPackage.ExportKind == ExportKind.RecordingSession
                    ? "recording_screenshots" : "scan_screenshots";

            var screenshotsDir = Path.Combine(outputDirectory, screenshotsSubfolder);
            Directory.CreateDirectory(screenshotsDir);

            foreach (var screenshot in screenshots)
            {
                if (File.Exists(screenshot.WorkingFilePath))
                {
                    var dest = Path.Combine(screenshotsDir, screenshot.RelativeFilePath);
                    File.Copy(screenshot.WorkingFilePath, dest, overwrite: true);
                }
            }

            var exportPath = Path.Combine(outputDirectory, exportFileName);
            var json = JsonSerializer.Serialize(exportPackage, JsonOptions);
            await File.WriteAllTextAsync(exportPath, json, ct);

            _logger.LogInformation("Export written to {Path}", exportPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write export to {Dir}", outputDirectory);
            return Result.Failure(FailureReason.DiskWriteFailed, ex.Message);
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 50 ? name[..50] : name;
    }
}