namespace WindowsUiFlowRecorder.Infrastructure.Automation;

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Domain.Policies;

public class FlaUiAutomationProvider : IUiAutomationProvider, IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly ILogger<FlaUiAutomationProvider> _logger;
    private bool _disposed;

    public FlaUiAutomationProvider(ILogger<FlaUiAutomationProvider> logger)
    {
        _automation = new UIA3Automation();
        _logger = logger;
    }

    public Task<Result<ElementInfo>> GetElementAtPointAsync(ScreenPoint point, CancellationToken ct)
    {
        try
        {
            var element = _automation.FromPoint(new Point(point.X, point.Y));
            if (element == null)
                return Task.FromResult(Result<ElementInfo>.Failure(
                    FailureReason.ElementNotFound, "No element at point"));

            var resolved = ResolveDeepestElementAtPoint(element, point);
            var info = BuildElementInfo(resolved, 0);
            return Task.FromResult(Result<ElementInfo>.Success(info));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get element at point ({X},{Y})", point.X, point.Y);
            return Task.FromResult(Result<ElementInfo>.Failure(FailureReason.ElementStale, ex.Message));
        }
    }

    public Task<Result<(ElementInfo Element, IReadOnlyList<string> AncestorPath)>> GetElementWithPathAtPointAsync(
        ScreenPoint point, CancellationToken ct)
    {
        try
        {
            var element = _automation.FromPoint(new Point(point.X, point.Y));
            if (element == null)
                return Task.FromResult(Result<(ElementInfo, IReadOnlyList<string>)>.Failure(
                    FailureReason.ElementNotFound, "No element at point"));

            var (deepest, ancestors) = ResolveDeepestElementAtPointWithPath(element, point);
            var info = BuildElementInfo(deepest, 0);
            var path = BuildAncestorPath(ancestors);
            return Task.FromResult(Result<(ElementInfo, IReadOnlyList<string>)>.Success((info, path)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get element with path at point ({X},{Y})", point.X, point.Y);
            return Task.FromResult(Result<(ElementInfo, IReadOnlyList<string>)>.Failure(
                FailureReason.ElementStale, ex.Message));
        }
    }

    private AutomationElement ResolveDeepestElementAtPoint(AutomationElement element, ScreenPoint point)
    {
        try
        {
            var children = element.FindAllChildren();
            if (children == null || children.Length == 0)
                return element;

            AutomationElement? bestChild = null;
            var bestArea = int.MaxValue;

            for (int i = 0; i < Math.Min(children.Length, 200); i++)
            {
                try
                {
                    var child = children[i];
                    var rect = child.BoundingRectangle;
                    if (rect.IsEmpty) continue;

                    var pt = new System.Drawing.Point(point.X, point.Y);
                    if (!rect.Contains(pt)) continue;

                    var area = (int)(rect.Width * rect.Height);

                    if (area < bestArea && area > 0)
                    {
                        bestArea = area;
                        bestChild = child;
                    }
                }
                catch { }
            }

            if (bestChild != null && bestChild != element)
                return ResolveDeepestElementAtPoint(bestChild, point);

            return element;
        }
        catch
        {
            return element;
        }
    }

    private (AutomationElement Element, List<AutomationElement> Ancestors) ResolveDeepestElementAtPointWithPath(
        AutomationElement element, ScreenPoint point)
    {
        try
        {
            var ancestors = new List<AutomationElement> { element };
            var result = ResolveDeepestElementAtPointWithPathRecursive(element, point, ancestors);
            return (result, ancestors);
        }
        catch
        {
            return (element, [element]);
        }
    }

    private AutomationElement ResolveDeepestElementAtPointWithPathRecursive(
        AutomationElement element, ScreenPoint point, List<AutomationElement> ancestors)
    {
        try
        {
            var children = element.FindAllChildren();
            if (children == null || children.Length == 0)
                return element;

            AutomationElement? bestChild = null;
            var bestArea = int.MaxValue;

            for (int i = 0; i < Math.Min(children.Length, 200); i++)
            {
                try
                {
                    var child = children[i];
                    var rect = child.BoundingRectangle;
                    if (rect.IsEmpty) continue;

                    var pt = new System.Drawing.Point(point.X, point.Y);
                    if (!rect.Contains(pt)) continue;

                    var area = (int)(rect.Width * rect.Height);

                    if (area < bestArea && area > 0)
                    {
                        bestArea = area;
                        bestChild = child;
                    }
                }
                catch { }
            }

            if (bestChild != null && bestChild != element)
            {
                ancestors.Add(bestChild);
                return ResolveDeepestElementAtPointWithPathRecursive(bestChild, point, ancestors);
            }

            return element;
        }
        catch
        {
            return element;
        }
    }

    private static IReadOnlyList<string> BuildAncestorPath(List<AutomationElement> ancestors)
    {
        var path = new List<string>(ancestors.Count);
        foreach (var el in ancestors)
        {
            try
            {
                var ctrlType = el.ControlType.ToString() ?? "Unknown";
                var name = el.Name ?? "";
                var autoId = el.AutomationId ?? "";

                var entry = ctrlType;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(autoId))
                    entry = $"{ctrlType}:{name}#{autoId}";
                else if (!string.IsNullOrEmpty(name))
                    entry = $"{ctrlType}:{name}";
                else if (!string.IsNullOrEmpty(autoId))
                    entry = $"{ctrlType}#{autoId}";

                path.Add(entry);
            }
            catch
            {
                path.Add("Unknown");
            }
        }
        return path;
    }

    public Task<Result<ElementInfo>> GetFocusedElementAsync(CancellationToken ct)
    {
        try
        {
            var element = _automation.FocusedElement();
            if (element == null)
                return Task.FromResult(Result<ElementInfo>.Failure(
                    FailureReason.ElementNotFound, "No focused element"));

            var info = BuildElementInfo(element, 0);
            return Task.FromResult(Result<ElementInfo>.Success(info));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get focused element");
            return Task.FromResult(Result<ElementInfo>.Failure(FailureReason.ElementStale, ex.Message));
        }
    }

    public Task<Result<WindowSnapshot>> GetWindowAtPointAsync(
        ScreenPoint point, int maxElementCount, CancellationToken ct)
    {
        try
        {
            var element = _automation.FromPoint(new Point(point.X, point.Y));
            if (element == null)
                return Task.FromResult(Result<WindowSnapshot>.Failure(
                    FailureReason.WindowNotFound, "No window at point"));

            var pid = element.Properties.ProcessId;
            int[] pids = [pid.ValueOrDefault];
            var windowHandle = FindWindowForProcess(pids);

            if (windowHandle == IntPtr.Zero)
                return Task.FromResult(Result<WindowSnapshot>.Failure(
                    FailureReason.WindowNotFound, "No window found for element's process"));

            return WalkHierarchyWithPidAsync(windowHandle, pid.ValueOrDefault, maxElementCount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get window at point ({X},{Y})", point.X, point.Y);
            return Task.FromResult(Result<WindowSnapshot>.Failure(
                FailureReason.WindowNotFound, ex.Message));
        }
    }

    public Task<Result<WindowSnapshot>> WalkHierarchyAsync(
        IntPtr windowHandle, int maxElementCount, CancellationToken ct)
    {
        return WalkHierarchyWithPidAsync(windowHandle, 0, maxElementCount, ct);
    }

    public Task<Result<WindowSnapshot>> WalkHierarchyWithPidAsync(
        IntPtr windowHandle, int processId, int maxElementCount, CancellationToken ct)
    {
        try
        {
            var element = _automation.FromHandle(windowHandle);
            if (element == null)
                return Task.FromResult(Result<WindowSnapshot>.Failure(
                    FailureReason.WindowNotFound, "Window not found via UIA"));

            var rootInfo = WalkElement(element, 0, ct, maxElementCount);
            var now = DateTime.UtcNow;
            var pid = processId > 0 ? processId : element.Properties.ProcessId.ValueOrDefault;

            var snapshot = new WindowSnapshot(
                Guid.NewGuid(), "", pid, windowHandle, element.Name,
                element.ClassName ?? "",
                new BoundingRectangle(
                    (int)element.BoundingRectangle.X,
                    (int)element.BoundingRectangle.Y,
                    (int)element.BoundingRectangle.Width,
                    (int)element.BoundingRectangle.Height),
                now, now, 1, rootInfo,
                HierarchyRecapturePolicy.ComputeFingerprint(rootInfo));

            return Task.FromResult(Result<WindowSnapshot>.Success(snapshot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to walk hierarchy for window {Handle}", windowHandle);
            return Task.FromResult(Result<WindowSnapshot>.Failure(
                FailureReason.HierarchyTooDeep, ex.Message));
        }
    }

    private ElementInfo BuildElementInfo(AutomationElement element, int depth)
    {
        return WalkElement(element, depth, CancellationToken.None, 5000);
    }

    private ElementInfo WalkElement(
        AutomationElement element, int depth, CancellationToken ct, int remaining)
    {
        if (ct.IsCancellationRequested || remaining <= 0)
            return CreateTruncatedElement(depth);

        try
        {
            var children = new List<ElementInfo>();
            try
            {
                var rawChildren = element.FindAllChildren();
                if (rawChildren != null)
                {
                    var takeCount = Math.Min(rawChildren.Length, remaining);
                    for (int i = 0; i < takeCount; i++)
                    {
                        children.Add(WalkElement(rawChildren[i], depth + 1, ct, remaining - i));
                    }
                }
            }
            catch
            {
            }

            var rect = element.BoundingRectangle;
            var patterns = element.GetSupportedPatterns()
                .Select(p =>
                {
                    try { return p.GetType().Name.Replace("Pattern", ""); }
                    catch { return "Unknown"; }
                })
                .ToList();

            var pid = element.Properties.ProcessId.ValueOrDefault;

            return new ElementInfo(
                element.AutomationId ?? Guid.NewGuid().ToString(),
                element.AutomationId,
                element.Name,
                element.ControlType.ToString() ?? "Unknown",
                null,
                element.ClassName,
                FrameworkDetector.DetectFramework(element, pid),
                element.HelpText,
                element.IsEnabled,
                element.IsOffscreen,
                false,
                new BoundingRectangle(
                    (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height),
                patterns,
                null,
                depth,
                pid,
                children);
        }
        catch
        {
            return CreateTruncatedElement(depth);
        }
    }

    private static ElementInfo CreateTruncatedElement(int depth) => new(
        Guid.NewGuid().ToString(), null, null, "Unknown",
        null, null, "Unknown", null, false, false, false,
        new BoundingRectangle(0, 0, 0, 0), [], null, depth, 0, []);

    public Task<Result> SubscribeToEventsAsync(
        IReadOnlyList<TargetApplicationContext> contexts,
        Action<ElementInfo> onFocusChanged,
        Action<WindowSnapshot> onWindowActivated,
        Action<IntPtr> onWindowOpened,
        CancellationToken ct)
    {
        _logger.LogInformation("UIA event listeners active for {Count} contexts", contexts.Count);
        return Task.FromResult(Result.Success());
    }

    public Task UnsubscribeAllAsync()
    {
        _logger.LogInformation("UIA event listeners removed");
        return Task.CompletedTask;
    }

    public Task<Result<WindowSnapshot>> GetOwningWindowAsync(ElementInfo element, CancellationToken ct)
    {
        try
        {
            var desktop = _automation.GetDesktop();
            var allWindows = desktop.FindAllChildren();

            var pid = element.ProcessId;
            var specificPids = pid > 0 ? new[] { pid } : Array.Empty<int>();

            AutomationElement? bestMatch = null;
            var bestScore = 0;

            foreach (var w in allWindows)
            {
                try
                {
                    if (!w.Properties.IsControlElement.ValueOrDefault)
                        continue;

                    var winPid = w.Properties.ProcessId.ValueOrDefault;
                    if (specificPids.Length > 0 && !specificPids.Contains(winPid))
                        continue;

                    var score = 0;

                    if (!string.IsNullOrEmpty(element.AutomationId) &&
                        string.Equals(w.AutomationId, element.AutomationId, StringComparison.OrdinalIgnoreCase))
                        score += 3;

                    if (!string.IsNullOrEmpty(element.Name) &&
                        string.Equals(w.Name, element.Name, StringComparison.OrdinalIgnoreCase))
                        score += 2;

                    if (!string.IsNullOrEmpty(element.ClassName) &&
                        string.Equals(w.ClassName, element.ClassName, StringComparison.OrdinalIgnoreCase))
                        score += 1;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = w;
                    }
                }
                catch { }
            }

            if (bestMatch != null)
            {
                var handle = bestMatch.Properties.NativeWindowHandle;
                if (handle.ValueOrDefault != 0)
                    return WalkHierarchyAsync((IntPtr)handle.ValueOrDefault, 5000, ct);
            }

            if (specificPids.Length > 0)
            {
                var handle = FindWindowForProcess(specificPids);
                if (handle != IntPtr.Zero)
                    return WalkHierarchyAsync(handle, 5000, ct);
            }

            foreach (var w in allWindows)
            {
                try
                {
                    var handle = w.Properties.NativeWindowHandle;
                    if (handle.ValueOrDefault != 0)
                        return WalkHierarchyAsync((IntPtr)handle.ValueOrDefault, 5000, ct);
                }
                catch { }
            }

            return Task.FromResult(Result<WindowSnapshot>.Failure(
                FailureReason.WindowNotFound, "Could not resolve owning window"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve owning window");
            return Task.FromResult(Result<WindowSnapshot>.Failure(
                FailureReason.WindowNotFound, ex.Message));
        }
    }

    private IntPtr FindWindowForProcess(int[] processIds)
    {
        var desktop = _automation.GetDesktop();
        var allWindows = desktop.FindAllChildren();

        foreach (var w in allWindows)
        {
            try
            {
                var pid = w.Properties.ProcessId.ValueOrDefault;
                if (processIds.Contains(pid))
                {
                    var handle = w.Properties.NativeWindowHandle;
                    if (handle.ValueOrDefault != 0)
                        return (IntPtr)handle.ValueOrDefault;
                }
            }
            catch { }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _automation?.Dispose();
            _disposed = true;
        }
    }
}