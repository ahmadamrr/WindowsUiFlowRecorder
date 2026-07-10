namespace WindowsUiFlowRecorder.Application.Export;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ExportService : IExportService
{
    private readonly IExportWriter _writer;
    private readonly ILogger<ExportService> _logger;

    private const string CurrentSchemaVersion = "1.1.0";
    private readonly int _recorderProcessId;

    public ExportService(IExportWriter writer, ILogger<ExportService> logger)
    {
        _writer = writer;
        _logger = logger;
        _recorderProcessId = Environment.ProcessId;
    }

    public async Task<Result> ExportSessionAsync(
        RecordingSession session, string outputDirectory, CancellationToken ct)
    {
        var package = MapSessionToExport(session);

        var validation = ValidateSchema(package);
        if (!validation.IsSuccess)
            return validation;

        var screenshots = CollectScreenshots(session);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sessionName = SanitizeFileName(session.Name);
        var exportFile = $"recording_{sessionName}_{timestamp}.json";
        return await _writer.WriteExportAsync(package, outputDirectory, screenshots, ct,
            exportFile, "recording_screenshots");
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

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var exportFile = $"scan_{timestamp}.json";
        return await _writer.WriteExportAsync(package, outputDirectory, [], ct,
            exportFile, "scan_screenshots");
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unnamed";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 50 ? name[..50] : name;
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
            .Where(a => a.TargetElement?.ProcessId != _recorderProcessId)
            .Select(a => new RecordedActionExport(
                a.ActionId, a.SequenceNumber, a.TimestampUtc,
                a.ActionType.ToString(), a.ApplicationTag, a.WindowId,
                a.TargetElement != null ? MapElement(a.TargetElement) : null,
                a.ElementPath, a.ScreenPoint, a.DragStartPoint,
                a.EnteredText, a.KeyName, a.PreviousWindowId, a.ScreenshotId))
            .ToList();

        var targetPids = session.TargetApplicationContexts
            .Select(c => c.ProcessId)
            .ToHashSet();

        var windows = session.Windows.Values
            .Where(w => w.ProcessId != _recorderProcessId && targetPids.Contains(w.ProcessId))
            .Select(MapWindow)
            .ToList();

        var scope = DetermineHierarchyExportScope(session);
        MarkInteractedElements(windows, session.Actions);
        windows = PruneHierarchy(windows, scope);
        windows = windows.Where(w => w.RootElement != null).ToList();

        var screenshotsSubfolder = "recording_screenshots";

        var screenshotInfos = session.Screenshots
            .Select(s => new ScreenshotInformation(
                s.ScreenshotId, $"{screenshotsSubfolder}/{s.RelativeFilePath}", s.Scope.ToString(),
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
    e.ClassName, e.FrameworkId, e.HelpText, e.IsEnabled, e.IsOffscreen, e.IsKeyboardFocusable,
        e.BoundingRectangle, e.SupportedPatterns, e.ValueOrText, e.DepthInTree,
        e.ProcessId, false, 0, [],
        e.Children.Select(MapElement).ToList());

    private static HierarchyExportScope DetermineHierarchyExportScope(RecordingSession session)
    {
        return HierarchyExportScope.InteractedElementsWithAncestorPath;
    }

    private static void MarkInteractedElements(
        List<WindowInformation> windows, List<RecordedAction> actions)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            var updatedRoot = MarkNodes(win.RootElement, actions, win.WindowId);
            windows[i] = win with { RootElement = updatedRoot };
        }
    }

    private static ElementInformation MarkNodes(
        ElementInformation node, List<RecordedAction> actions, Guid windowId)
    {
        var matchingActionIds = new List<Guid>();

        foreach (var action in actions)
        {
            if (action.WindowId != windowId) continue;
            if (action.TargetElement == null) continue;

            if (MatchesAction(node, action.TargetElement))
                matchingActionIds.Add(action.ActionId);
        }

        matchingActionIds.Sort((a, b) =>
        {
            var seqA = actions.FirstOrDefault(ra => ra.ActionId == a)?.SequenceNumber ?? 0;
            var seqB = actions.FirstOrDefault(ra => ra.ActionId == b)?.SequenceNumber ?? 0;
            return seqA.CompareTo(seqB);
        });

        var updatedChildren = node.Children
            .Select(c => MarkNodes(c, actions, windowId))
            .ToList();

        return node with
        {
            WasInteractedWith = matchingActionIds.Count > 0,
            InteractionCount = matchingActionIds.Count,
            InteractedActionIds = matchingActionIds.AsReadOnly(),
            Children = updatedChildren.AsReadOnly()
        };
    }

    private static bool MatchesAction(ElementInformation node, ElementInfo target)
    {
        if (!string.IsNullOrEmpty(target.AutomationId) &&
            string.Equals(node.AutomationId, target.AutomationId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(node.ControlType, target.ControlType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Name ?? "", target.Name ?? "", StringComparison.Ordinal) &&
            BoundsWithinTolerance(node.BoundingRectangle, target.BoundingRectangle, 2))
            return true;

        return false;
    }

    private static bool BoundsWithinTolerance(
        BoundingRectangle a, BoundingRectangle b, int tolerance)
    {
        return Math.Abs(a.X - b.X) <= tolerance &&
               Math.Abs(a.Y - b.Y) <= tolerance &&
               Math.Abs(a.Width - b.Width) <= tolerance &&
               Math.Abs(a.Height - b.Height) <= tolerance;
    }

    private static List<WindowInformation> PruneHierarchy(
        List<WindowInformation> windows, HierarchyExportScope scope)
    {
        if (scope == HierarchyExportScope.FullTree)
            return windows;

        return windows
            .Select(w => w with { RootElement = PruneNode(w.RootElement, scope) })
            .ToList();
    }

    private static ElementInformation PruneNode(ElementInformation node, HierarchyExportScope scope)
    {
        if (scope == HierarchyExportScope.FullTree)
            return node;

        if (scope == HierarchyExportScope.InteractedElementsOnly)
        {
            if (node.WasInteractedWith)
                return node with { Children = new List<ElementInformation>().AsReadOnly() };

            return null!;
        }

        var prunedChildren = node.Children
            .Select(c => PruneNode(c, scope))
            .Where(c => c != null)
            .ToList();

        var hasInteractedDescendant = prunedChildren.Count > 0 || node.WasInteractedWith;

        if (!hasInteractedDescendant)
            return null!;

        if (node.WasInteractedWith)
            return node with { Children = prunedChildren.AsReadOnly() };

        return TrimToAncestor(node, prunedChildren.AsReadOnly());
    }

    private static ElementInformation TrimToAncestor(
        ElementInformation node, IReadOnlyList<ElementInformation> children)
    {
        return new ElementInformation(
            node.ElementId, node.AutomationId, node.Name,
            node.ControlType, node.LocalizedControlType, node.ClassName, node.FrameworkId,
            null, false, false, false,
            new BoundingRectangle(0, 0, 0, 0), [], null, 0, 0,
            false, 0, [],
            children);
    }
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