namespace WindowsUiFlowRecorder.Application.Export;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ExportService : IExportService
{
    private readonly IExportWriter _writer;
    private readonly ILogger<ExportService> _logger;

    public ExportService(IExportWriter writer, ILogger<ExportService> logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task<Result> ExportSessionAsync(
        RecordingSession session, string outputDirectory, CancellationToken ct)
    {
        var package = MapSessionToExport(session);
        return await WriteExportAsync(package, session.SessionId, outputDirectory, ct);
    }

    public async Task<Result> ExportStandaloneScanAsync(
        WindowSnapshot snapshot, string outputDirectory, CancellationToken ct)
    {
        var scanExport = new StandaloneScanExport(
            Guid.NewGuid(), DateTime.UtcNow,
            new TargetApplicationInformation(snapshot.ApplicationTag, "", snapshot.ProcessId,
                1, snapshot.FirstCapturedAtUtc, null, "NotTerminated"),
            [MapWindow(snapshot)]);

        var package = new ExportPackage(
            "1.0.0", "0.1.0", DateTime.UtcNow,
            ExportKind.StandaloneScan, null, scanExport);

        return await _writer.WriteExportAsync(package, outputDirectory, [], ct);
    }

    private ExportPackage MapSessionToExport(RecordingSession session)
    {
        var targetApps = session.TargetApplicationContexts
            .Select(c => new TargetApplicationInformation(
                c.ApplicationTag, c.ExecutablePath, c.ProcessId, c.LaunchOrder,
                c.LaunchedAtUtc, c.TerminatedAtUtc,
                c.TerminationReason.ToString()))
            .ToList();

        var actions = session.Actions
            .Select(a => new RecordedActionExport(
                a.ActionId, a.SequenceNumber, a.TimestampUtc,
                a.ActionType.ToString(), a.ApplicationTag, a.WindowId,
                a.TargetElement != null ? MapElement(a.TargetElement) : null,
                a.ElementPath, a.ScreenPoint, a.DragStartPoint,
                a.EnteredText, a.KeyName, a.PreviousWindowId, a.ScreenshotId))
            .ToList();

        var windows = session.Windows.Values
            .Select(MapWindow)
            .ToList();

        var duration = session.StoppedAtUtc.HasValue && session.StartedAtUtc.HasValue
            ? (int)(session.StoppedAtUtc.Value - session.StartedAtUtc.Value).TotalSeconds
            : 0;

        var recordingExport = new RecordingSessionExport(
            session.SessionId, session.Name, session.Note,
            session.CreatedAtUtc, session.StartedAtUtc ?? DateTime.UtcNow,
            session.StoppedAtUtc ?? DateTime.UtcNow, duration,
            "UserStopped", null, targetApps, actions, windows, []);

        return new ExportPackage(
            "1.0.0", "0.1.0", DateTime.UtcNow,
            ExportKind.RecordingSession, recordingExport, null);
    }

    private async Task<Result> WriteExportAsync(
        ExportPackage package, Guid sessionId,
        string outputDirectory, CancellationToken ct)
    {
        var screenshots = new List<ScreenshotReference>();
        return await _writer.WriteExportAsync(package, outputDirectory, screenshots, ct);
    }

    private static WindowInformation MapWindow(WindowSnapshot ws) => new(
        ws.WindowId, ws.ApplicationTag, ws.ProcessId, ws.Title, ws.ClassName,
        ws.BoundingRectangle, ws.FirstCapturedAtUtc, ws.LastUpdatedAtUtc,
        ws.CaptureCount, MapElement(ws.RootElement));

    private static ElementInformation MapElement(ElementInfo e) => new(
        e.ElementId, e.AutomationId, e.Name, e.ControlType, e.LocalizedControlType,
        e.ClassName, e.HelpText, e.IsEnabled, e.IsOffscreen, e.IsKeyboardFocusable,
        e.BoundingRectangle, e.SupportedPatterns, e.ValueOrText, e.DepthInTree,
        e.Children.Select(MapElement).ToList());
}