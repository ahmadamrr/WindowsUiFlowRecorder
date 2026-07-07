namespace WindowsUiFlowRecorder.Application.Export;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ExportService : IExportService
{
    private readonly IExportWriter _writer;
    private readonly ILogger<ExportService> _logger;

    private const string CurrentSchemaVersion = "1.0.0";

    public ExportService(IExportWriter writer, ILogger<ExportService> logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task<Result> ExportSessionAsync(
        RecordingSession session, string outputDirectory, CancellationToken ct)
    {
        var package = MapSessionToExport(session);

        var validation = ValidateSchema(package);
        if (!validation.IsSuccess)
            return validation;

        var screenshots = CollectScreenshots(session);
        return await _writer.WriteExportAsync(package, outputDirectory, screenshots, ct);
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
            CurrentSchemaVersion, "0.1.0", DateTime.UtcNow,
            ExportKind.StandaloneScan, null, scanExport);

        var validation = ValidateSchema(package);
        if (!validation.IsSuccess)
            return validation;

        return await _writer.WriteExportAsync(package, outputDirectory, [], ct);
    }

    private static Result ValidateSchema(ExportPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.SchemaVersion))
            return Result.Failure(FailureReason.ExportValidationFailed,
                "SchemaVersion is required");

        if (!SemanticVersion.TryParse(package.SchemaVersion, out var version))
            return Result.Failure(FailureReason.ExportValidationFailed,
                $"SchemaVersion '{package.SchemaVersion}' is not valid semver");

        if (version.Major == 0)
            return Result.Failure(FailureReason.ExportValidationFailed,
                $"SchemaVersion '{package.SchemaVersion}' is a pre-release version; " +
                "only MAJOR >= 1 is valid for production exports");

        if (package.ExportKind == ExportKind.RecordingSession && package.RecordingSession == null)
            return Result.Failure(FailureReason.ExportValidationFailed,
                "ExportKind is RecordingSession but RecordingSession data is null");

        if (package.ExportKind == ExportKind.StandaloneScan && package.StandaloneScan == null)
            return Result.Failure(FailureReason.ExportValidationFailed,
                "ExportKind is StandaloneScan but StandaloneScan data is null");

        if (package.RecordingSession != null)
        {
            var session = package.RecordingSession;
            if (session.Actions == null)
                return Result.Failure(FailureReason.ExportValidationFailed, "Actions list is null");
            if (session.Windows == null)
                return Result.Failure(FailureReason.ExportValidationFailed, "Windows list is null");
            if (session.TargetApplications == null)
                return Result.Failure(FailureReason.ExportValidationFailed, "TargetApplications list is null");

            foreach (var action in session.Actions)
            {
                if (action.ActionType == null)
                    return Result.Failure(FailureReason.ExportValidationFailed,
                        $"Action {action.ActionId} has null ActionType");
            }
        }

        return Result.Success();
    }

    private static List<ScreenshotReference> CollectScreenshots(RecordingSession session)
    {
        var screenshots = new List<ScreenshotReference>();
        var seen = new HashSet<Guid>();

        foreach (var action in session.Actions)
        {
            if (action.ScreenshotId.HasValue && seen.Add(action.ScreenshotId.Value))
            {
                var matched = session.Screenshots.Find(s => s.ScreenshotId == action.ScreenshotId.Value);
                if (matched != null)
                    screenshots.Add(matched);
            }
        }

        return screenshots;
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

        var screenshotInfos = session.Screenshots
            .Select(s => new ScreenshotInformation(
                s.ScreenshotId, $"screenshots/{s.RelativeFilePath}", s.Scope.ToString(),
                s.Format.ToString(), s.Width, s.Height, s.CapturedAtUtc,
                s.AssociatedActionId, s.AssociatedWindowId))
            .ToList();

        var duration = session.StoppedAtUtc.HasValue && session.StartedAtUtc.HasValue
            ? (int)(session.StoppedAtUtc.Value - session.StartedAtUtc.Value).TotalSeconds
            : 0;

        var terminationReason = DetermineTerminationReason(session);

        var recordingExport = new RecordingSessionExport(
            session.SessionId, session.Name, session.Note,
            session.CreatedAtUtc, session.StartedAtUtc ?? DateTime.UtcNow,
            session.StoppedAtUtc ?? DateTime.UtcNow, duration,
            terminationReason, null, targetApps, actions, windows, screenshotInfos);

        return new ExportPackage(
            CurrentSchemaVersion, "0.1.0", DateTime.UtcNow,
            ExportKind.RecordingSession, recordingExport, null);
    }

    private static string DetermineTerminationReason(RecordingSession session)
    {
        if (session.TargetApplicationContexts.Count == 0)
            return "Unknown";

        var allCrashed = session.TargetApplicationContexts.All(
            c => c.TerminationReason == TargetTerminationReason.ProcessCrashed);
        if (allCrashed && session.TargetApplicationContexts.Any(
                c => c.TerminationReason == TargetTerminationReason.ProcessCrashed))
            return "AllTargetsCrashedOrExited";

        return "UserStopped";
    }

    private static WindowInformation MapWindow(WindowSnapshot ws) => new(
        ws.WindowId, ws.ApplicationTag, ws.ProcessId, ws.Title, ws.ClassName,
        ws.BoundingRectangle, ws.FirstCapturedAtUtc, ws.LastUpdatedAtUtc,
        ws.CaptureCount, MapElement(ws.RootElement));

    private static ElementInformation MapElement(ElementInfo e) => new(
        e.ElementId, e.AutomationId, e.Name, e.ControlType, e.LocalizedControlType,
        e.ClassName, e.HelpText, e.IsEnabled, e.IsOffscreen, e.IsKeyboardFocusable,
        e.BoundingRectangle, e.SupportedPatterns, e.ValueOrText, e.DepthInTree,
        e.ProcessId,
        e.Children.Select(MapElement).ToList());
}

internal static class SemanticVersion
{
    public static bool TryParse(string input, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var parts = input.Split('.');
        if (parts.Length < 2) return false;

        var cleaned = parts[0].Split('-')[0];
        if (!int.TryParse(cleaned, out var major)) return false;

        var cleanedMinor = parts[1].Split('-')[0];
        if (!int.TryParse(cleanedMinor, out var minor)) return false;

        var patch = 0;
        if (parts.Length > 2)
        {
            var cleanedPatch = parts[2].Split('-')[0];
            int.TryParse(cleanedPatch, out patch);
        }

        version = (major, minor, patch);
        return true;
    }
}