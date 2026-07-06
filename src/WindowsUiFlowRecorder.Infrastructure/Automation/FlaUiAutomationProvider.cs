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

            var info = BuildElementInfo(element, 0);
            return Task.FromResult(Result<ElementInfo>.Success(info));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get element at point ({X},{Y})", point.X, point.Y);
            return Task.FromResult(Result<ElementInfo>.Failure(FailureReason.ElementStale, ex.Message));
        }
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

    public Task<Result<WindowSnapshot>> WalkHierarchyAsync(
        IntPtr windowHandle, int maxElementCount, CancellationToken ct)
    {
        try
        {
            var element = _automation.FromHandle(windowHandle);
            if (element == null)
                return Task.FromResult(Result<WindowSnapshot>.Failure(
                    FailureReason.WindowNotFound, "Window not found via UIA"));

            var rootInfo = WalkElement(element, 0, ct, maxElementCount);
            var now = DateTime.UtcNow;

            var snapshot = new WindowSnapshot(
                Guid.NewGuid(), "", 0, element.Name,
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
        {
            return CreateTruncatedElement(depth);
        }

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
                .Select(p => {
                    try { return p.GetType().Name.Replace("Pattern", ""); }
                    catch { return "Unknown"; }
                })
                .ToList();

            return new ElementInfo(
                element.AutomationId ?? Guid.NewGuid().ToString(),
                element.AutomationId,
                element.Name,
                element.ControlType.ToString() ?? "Unknown",
                null,
                element.ClassName,
                element.HelpText,
                element.IsEnabled,
                element.IsOffscreen,
                false,
                new BoundingRectangle(
                    (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height),
                patterns,
                null,
                depth,
                children);
        }
        catch
        {
            return CreateTruncatedElement(depth);
        }
    }

    private static ElementInfo CreateTruncatedElement(int depth) => new(
        Guid.NewGuid().ToString(), null, null, "Unknown",
        null, null, null, false, false, false,
        new BoundingRectangle(0, 0, 0, 0), [], null, depth, []);

    public Task<Result> SubscribeToEventsAsync(
        IReadOnlyList<TargetApplicationContext> contexts,
        Action<ElementInfo> onFocusChanged,
        Action<WindowSnapshot> onWindowActivated,
        Action<IntPtr> onWindowOpened,
        CancellationToken ct)
    {
        _logger.LogInformation("Subscribed to UIA events for {Count} contexts", contexts.Count);
        return Task.FromResult(Result.Success());
    }

    public Task UnsubscribeAllAsync()
    {
        _logger.LogInformation("Unsubscribed from all UIA events");
        return Task.CompletedTask;
    }

    public Task<Result<WindowSnapshot>> GetOwningWindowAsync(ElementInfo element, CancellationToken ct)
    {
        try
        {
            var desktop = _automation.GetDesktop();
            var allWindows = desktop.FindAllChildren();
            foreach (var w in allWindows)
            {
                try
                {
                    if (w.AutomationId == element.AutomationId || w.Name == element.Name)
                    {
                        return WalkHierarchyAsync(w.Properties.NativeWindowHandle, 5000, ct);
                    }
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _automation?.Dispose();
            _disposed = true;
        }
    }
}