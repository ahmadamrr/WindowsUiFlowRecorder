namespace WindowsUiFlowRecorder.Application.Recording;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Application.Launching;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Application.Settings;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Domain.Policies;

public class RecordingSessionService : IRecordingSessionService, IDisposable
{
    private readonly IApplicationLaunchOrchestrator _launchOrchestrator;
    private readonly IGlobalInputHook _inputHook;
    private readonly IUiAutomationProvider _uiAutomation;
    private readonly IScreenshotCapturer _screenshotCapturer;
    private readonly IProcessLaunchMonitor _processMonitor;
    private readonly IExportService _exportService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RecordingSessionService> _logger;

    private RecordingSession? _session;
    private ApplicationLaunchChain? _launchChain;
    private CancellationTokenSource? _captureCts;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<RawInputEvent> _inputQueue = new();
    private readonly ConcurrentDictionary<IntPtr, Guid> _windowHandleMap = new();
    private int _sequenceNumber;
    private Task? _captureLoopTask;
    private Task? _heartbeatTask;

    private const int HeartbeatThresholdSeconds = 15;
    private readonly int _recorderProcessId;

    public RecordingSessionState CurrentState { get; private set; } = RecordingSessionState.Idle;
    public RecordingSession? CurrentSession => _session;
    public event Action<RecordingSessionState>? StateChanged;
    public event Action<string>? ErrorOccurred;

    public RecordingSessionService(
        IApplicationLaunchOrchestrator launchOrchestrator,
        IGlobalInputHook inputHook,
        IUiAutomationProvider uiAutomation,
        IScreenshotCapturer screenshotCapturer,
        IProcessLaunchMonitor processMonitor,
        IExportService exportService,
        ISessionRepository sessionRepository,
        ISettingsService settingsService,
        ILogger<RecordingSessionService> logger)
    {
        _launchOrchestrator = launchOrchestrator;
        _inputHook = inputHook;
        _uiAutomation = uiAutomation;
        _screenshotCapturer = screenshotCapturer;
        _processMonitor = processMonitor;
        _exportService = exportService;
        _sessionRepository = sessionRepository;
        _settingsService = settingsService;
        _logger = logger;
        _recorderProcessId = Environment.ProcessId;
    }

    public Task<Result> PrepareAsync(ApplicationLaunchChain launchChain, CancellationToken ct)
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Idle)
                return Task.FromResult(Result.Failure(FailureReason.Unknown,
                    "Session must be Idle to prepare"));

            _launchChain = launchChain;
            SetState(RecordingSessionState.Configuring);
            _logger.LogInformation("Session configured with {StepCount} launch steps", launchChain.Steps.Count);
            return Task.FromResult(Result.Success());
        }
    }

    public async Task<Result> StartRecordingAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Configuring)
                return Result.Failure(FailureReason.Unknown,
                    "Session must be Configuring to start recording");
            SetState(RecordingSessionState.LaunchingChain);
        }

        if (_launchChain == null)
        {
            SetState(RecordingSessionState.LaunchFailed);
            var msg = "No launch chain configured";
            ErrorOccurred?.Invoke(msg);
            return Result.Failure(FailureReason.InvalidLaunchChain, msg);
        }

        var settings = (await _settingsService.GetSettingsAsync()).Value;
        var pollInterval = settings?.DefaultReadinessPollIntervalMilliseconds ?? 250;

        var launchResult = await _launchOrchestrator.ExecuteLaunchChainAsync(
            _launchChain, pollInterval, ct);

        if (!launchResult.IsSuccess)
        {
            SetState(RecordingSessionState.LaunchFailed);
            var msg = $"Launch chain failed: {launchResult.ErrorMessage}";
            ErrorOccurred?.Invoke(msg);
            _logger.LogError("Launch chain failed: {Reason}", launchResult.ErrorMessage);
            return Result.Failure(launchResult.FailureReason!.Value, launchResult.ErrorMessage);
        }

        var contexts = launchResult.Value!;
        var processIds = contexts.Select(c => c.ProcessId).ToList();

        _session = new RecordingSession
        {
            SessionId = Guid.NewGuid(),
            Name = $"Session_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            State = RecordingSessionState.Recording,
            TargetApplicationContexts = [.. contexts],
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = DateTime.UtcNow,
            ApplicationProfileId = null
        };

        _captureCts = new CancellationTokenSource();

        _ = _processMonitor.SubscribeToExitEventsAsync(processIds, OnTargetProcessExited);

        await _inputHook.SubscribeAsync(OnRawInputEvent, ct);

        await _uiAutomation.SubscribeToEventsAsync(
            contexts,
            OnFocusChanged,
            OnWindowActivated,
            OnWindowOpened,
            ct);

        _captureLoopTask = Task.Run(() => CaptureProcessingLoopAsync(_captureCts.Token), ct);
        _heartbeatTask = Task.Run(() => HeartbeatMonitorAsync(_captureCts.Token), ct);

        SetState(RecordingSessionState.Recording);
        _logger.LogInformation("Recording started for session {SessionId} with {ContextCount} contexts",
            _session.SessionId, contexts.Count);

        return Result.Success();
    }

    public Result PauseSession()
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Recording)
                return Result.Failure(FailureReason.Unknown, "Session is not recording");

            SetState(RecordingSessionState.Paused);
            _logger.LogInformation("Session paused");
            return Result.Success();
        }
    }

    public Result ResumeSession()
    {
        lock (_lock)
        {
            if (CurrentState != RecordingSessionState.Paused)
                return Result.Failure(FailureReason.Unknown, "Session is not paused");

            SetState(RecordingSessionState.Recording);
            _logger.LogInformation("Session resumed");
            return Result.Success();
        }
    }

    public async Task<Result<RecordingSession>> StopSessionAsync(CancellationToken ct)
    {
        RecordingSession? session;
        lock (_lock)
        {
            if (CurrentState is not (RecordingSessionState.Recording or RecordingSessionState.Paused))
                return Result<RecordingSession>.Failure(FailureReason.Unknown,
                    "No active session to stop");

            session = _session;
            if (session == null)
                return Result<RecordingSession>.Failure(FailureReason.SessionNotFound,
                    "No session exists");

            SetState(RecordingSessionState.Stopped);
        }

        _captureCts?.Cancel();

        if (_captureLoopTask != null)
        {
            try { await _captureLoopTask.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { _logger.LogWarning("Capture loop did not exit within 10s timeout"); }
            catch (OperationCanceledException) { _logger.LogDebug("Capture loop was cancelled"); }
            catch (Exception ex) { _logger.LogError(ex, "Capture loop exited with error"); }
        }

        await _inputHook.UnsubscribeAsync();
        await _uiAutomation.UnsubscribeAllAsync();
        await _processMonitor.UnsubscribeAllAsync();

        session.StoppedAtUtc = DateTime.UtcNow;
        session.State = RecordingSessionState.Stopped;

        await _sessionRepository.SaveSessionAsync(session);

        _logger.LogInformation("Session {SessionId} stopped with {ActionCount} actions and {WindowCount} windows",
            session.SessionId, session.Actions.Count, session.Windows.Count);

        SetState(RecordingSessionState.Reviewing);
        return Result<RecordingSession>.Success(session);
    }

    public void ResetToIdle()
    {
        lock (_lock)
        {
            _session = null;
            _launchChain = null;
            _captureCts?.Cancel();
            _captureCts = null;
            SetState(RecordingSessionState.Idle);
        }
    }

    public SessionListItem? GetSessionSummary()
    {
        lock (_lock)
        {
            if (_session == null) return null;

            var contexts = _session.TargetApplicationContexts;
            var screenshotCount = _session.Actions.Count(a => a.ScreenshotId != null);

            return new SessionListItem(
                _session.SessionId,
                _session.Name,
                _session.CreatedAtUtc,
                (int)((_session.StoppedAtUtc ?? DateTime.UtcNow) - (_session.StartedAtUtc ?? DateTime.UtcNow)).TotalSeconds,
                contexts.Select(c => c.ApplicationTag).ToList(),
                _session.Actions.Count,
                _session.Windows.Count,
                screenshotCount,
                _session.Note
            );
        }
    }

    public async Task<Result> ExportSessionAsync(string outputDirectory, CancellationToken ct)
    {
        RecordingSession sessionForExport;
        lock (_lock)
        {
            if (CurrentState is not (RecordingSessionState.Reviewing or RecordingSessionState.Stopped or RecordingSessionState.Exported))
                return Result.Failure(FailureReason.Unknown, "Session must be in review state to export");

            if (_session == null)
                return Result.Failure(FailureReason.SessionNotFound, "No session to export");

            sessionForExport = new RecordingSession
            {
                SessionId = _session.SessionId,
                Name = _session.Name,
                Note = _session.Note,
                State = _session.State,
                ApplicationProfileId = _session.ApplicationProfileId,
                TargetApplicationContexts = [.. _session.TargetApplicationContexts],
                Actions = [.. _session.Actions],
                Windows = new Dictionary<Guid, WindowSnapshot>(_session.Windows),
                Screenshots = [.. _session.Screenshots],
                CreatedAtUtc = _session.CreatedAtUtc,
                StartedAtUtc = _session.StartedAtUtc,
                StoppedAtUtc = _session.StoppedAtUtc
            };
        }

        SetState(RecordingSessionState.Exporting);

        try
        {
            var result = await _exportService.ExportSessionAsync(sessionForExport, outputDirectory, ct);

            if (result.IsSuccess)
            {
                SetState(RecordingSessionState.Exported);
                _logger.LogInformation("Session exported to {Path}", outputDirectory);
            }
            else
            {
                SetState(RecordingSessionState.Reviewing);
                ErrorOccurred?.Invoke(result.ErrorMessage ?? "Export failed");
            }

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Collection was modified"))
        {
            _logger.LogError(ex,
                "Export failed - concurrent modification detected on snapshot. " +
                "Actions={Actions}, Windows={Windows}, Screenshots={Screenshots}",
                sessionForExport.Actions.Count, sessionForExport.Windows.Count,
                sessionForExport.Screenshots.Count);
            SetState(RecordingSessionState.Reviewing);
            ErrorOccurred?.Invoke("Export failed due to concurrent modification. Please try again.");
            return Result.Failure(FailureReason.Unknown, "Concurrent modification during export");
        }
    }

    private void OnRawInputEvent(RawInputEvent evt)
    {
        if (CurrentState != RecordingSessionState.Recording) return;

        if (evt.WindowHandle.HasValue && evt.WindowHandle.Value != IntPtr.Zero)
        {
            GetWindowThreadProcessId(evt.WindowHandle.Value, out var pid);
            if (pid == _recorderProcessId)
                return;
        }

        _inputQueue.Enqueue(evt);
    }

    private void OnFocusChanged(ElementInfo element)
    {
        if (CurrentState != RecordingSessionState.Recording) return;
        _inputQueue.Enqueue(new RawInputEvent(
            InputEventType.FocusGained, DateTime.UtcNow, null, null, false, IntPtr.Zero));
    }

    private void OnWindowActivated(WindowSnapshot snapshot)
    {
        if (CurrentState != RecordingSessionState.Recording) return;

        _ = TryUpdateWindowCaptureAsync(snapshot);

        _inputQueue.Enqueue(new RawInputEvent(
            InputEventType.WindowActivated, DateTime.UtcNow, null, null, false, IntPtr.Zero));
    }

    private async Task TryUpdateWindowCaptureAsync(WindowSnapshot snapshot)
    {
        try
        {
            if (snapshot.ProcessId == _recorderProcessId) return;

            var settings = (await _settingsService.GetSettingsAsync()).Value;
            var minInterval = GetMinimumRecaptureInterval(
                settings?.HierarchyRecaptureSensitivity ?? HierarchyRecaptureSensitivity.Medium);

            lock (_lock)
            {
                if (_session == null) return;

                var handle = snapshot.NativeWindowHandle;

                if (_windowHandleMap.TryGetValue(handle, out var existingId))
                {
                    if (_session.Windows.TryGetValue(existingId, out var existing))
                    {
                        var timeSinceLastCapture = DateTime.UtcNow - existing.LastUpdatedAtUtc;
                        var shouldRecapture = HierarchyRecapturePolicy.ShouldRecapture(
                            existing.StructuralFingerprint,
                            snapshot.StructuralFingerprint,
                            minInterval,
                            timeSinceLastCapture);

                        if (shouldRecapture)
                        {
                            var updated = snapshot with
                            {
                                WindowId = existingId,
                                FirstCapturedAtUtc = existing.FirstCapturedAtUtc,
                                CaptureCount = existing.CaptureCount + 1
                            };
                            _session.Windows[existingId] = updated;
                            _logger.LogDebug("Window re-captured (structural change): {Title} (capture #{Count})",
                                snapshot.Title, updated.CaptureCount);
                        }
                    }
                    else
                    {
                        _windowHandleMap.TryRemove(handle, out _);
                        _session.Windows[snapshot.WindowId] = snapshot;
                        _windowHandleMap[handle] = snapshot.WindowId;
                    }
                }
                else
                {
                    _session.Windows[snapshot.WindowId] = snapshot;
                    _windowHandleMap[handle] = snapshot.WindowId;
                    _logger.LogDebug("Window captured: {Title}", snapshot.Title);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check window re-capture for window");
        }
    }

    private void OnWindowOpened(IntPtr windowHandle)
    {
        _logger.LogDebug("New window opened: {Handle}", windowHandle);
    }

    private void OnTargetProcessExited(int processId)
    {
        _logger.LogWarning("Target process exited: {Pid}", processId);

        lock (_lock)
        {
            if (_session == null) return;

            var ctx = _session.TargetApplicationContexts
                .FirstOrDefault(c => c.ProcessId == processId);
            if (ctx == null) return;

            var index = _session.TargetApplicationContexts.IndexOf(ctx);
            _session.TargetApplicationContexts[index] = ctx with
            {
                IsActive = false,
                TerminatedAtUtc = DateTime.UtcNow,
                TerminationReason = TargetTerminationReason.ProcessCrashed
            };

            var allInactive = _session.TargetApplicationContexts.All(c => !c.IsActive);
            if (allInactive)
            {
                _logger.LogInformation("All target processes exited, auto-stopping session");
                _ = StopSessionInternalAsync("All target applications exited");
            }
        }
    }

    private async Task StopSessionInternalAsync(string reason)
    {
        try
        {
            await StopSessionAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-stop session: {Reason}", reason);
        }
    }

    private async Task CaptureProcessingLoopAsync(CancellationToken ct)
    {
        var coalesceWindow = new List<RawInputEvent>();
        RawInputEvent? lastEvent = null;
        var windowBatch = new HashSet<Guid>();
        var lastCoalesceReset = DateTime.UtcNow;
        var settings = (await _settingsService.GetSettingsAsync()).Value;
        var settingsRefreshCount = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (CurrentState != RecordingSessionState.Recording)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                if (++settingsRefreshCount % 50 == 0)
                {
                    var freshSettings = await _settingsService.GetSettingsAsync();
                    if (freshSettings.IsSuccess)
                        settings = freshSettings.Value;
                }

                if (_inputQueue.TryDequeue(out var evt))
                {
                    var now = DateTime.UtcNow;

                    if (ShouldStartNewCoalesceWindow(lastEvent, evt, now, lastCoalesceReset))
                    {
                        if (coalesceWindow.Count > 0)
                        {
                            await FlushCoalescedActionAsync(coalesceWindow, settings, ct);
                        }
                        coalesceWindow.Clear();
                        lastCoalesceReset = now;
                    }

                    coalesceWindow.Add(evt);
                    lastEvent = evt;
                }
                else
                {
                    if (coalesceWindow.Count > 0 &&
                        (DateTime.UtcNow - lastCoalesceReset).TotalMilliseconds > 1500)
                    {
                        await FlushCoalescedActionAsync(coalesceWindow, settings, ct);
                        coalesceWindow.Clear();
                        lastCoalesceReset = DateTime.UtcNow;
                    }

                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in capture processing loop");
                await Task.Delay(100, ct);
            }
        }

        if (coalesceWindow.Count > 0)
        {
            await FlushCoalescedActionAsync(coalesceWindow, settings, CancellationToken.None);
        }
    }

    private static bool ShouldStartNewCoalesceWindow(
        RawInputEvent? last, RawInputEvent current, DateTime now, DateTime lastReset)
    {
        if (last == null) return false;

        if (last.Value.EventType == InputEventType.MouseDown && current.EventType == InputEventType.MouseUp)
            return false;

        if (current.EventType == InputEventType.MouseDown && last.Value.EventType != InputEventType.MouseMove)
            return true;

        if (current.EventType == InputEventType.WindowActivated)
            return true;

        if ((now - lastReset).TotalMilliseconds > 1500)
            return true;

        return false;
    }

    private async Task FlushCoalescedActionAsync(
        List<RawInputEvent> events, Settings? settings, CancellationToken ct)
    {
        if (events.Count == 0 || _session == null) return;

        try
        {
            var first = events[0];
            var last = events[^1];

            ElementInfo? targetElement = null;
            Guid windowId = Guid.Empty;
            string applicationTag = "Unknown";
            int elementProcessId = 0;

            if (last.ScreenPosition.HasValue)
            {
                var maxElements = GetMaxElementCount(settings);
                var windowResult = await _uiAutomation.GetWindowAtPointAsync(
                    last.ScreenPosition.Value, maxElements, ct);
                if (windowResult.IsSuccess)
                {
                    var snapshot = windowResult.Value;
                    elementProcessId = snapshot.ProcessId;

                    if (elementProcessId == _recorderProcessId)
                        return;

                    windowId = snapshot.WindowId;
                    applicationTag = snapshot.ApplicationTag;
                    targetElement = snapshot.RootElement;
                    await ApplyWindowSnapshotAsync(snapshot, settings);
                }
                else
                {
                    var elementResult = await _uiAutomation.GetElementAtPointAsync(
                        last.ScreenPosition.Value, ct);
                    if (elementResult.IsSuccess)
                    {
                        targetElement = elementResult.Value;
                        elementProcessId = targetElement.ProcessId;
                    }
                }
            }
            else if (first.EventType is InputEventType.KeyDown or InputEventType.KeyUp or InputEventType.FocusGained)
            {
                var focusResult = await _uiAutomation.GetFocusedElementAsync(ct);
                if (focusResult.IsSuccess)
                {
                    targetElement = focusResult.Value;
                    elementProcessId = targetElement.ProcessId;
                }
            }
            else if (first.EventType == InputEventType.WindowActivated)
            {
                var handle = last.WindowHandle;
                if (handle.HasValue && handle.Value != IntPtr.Zero)
                {
                    var maxElements = GetMaxElementCount(settings);
                    var winResult = await _uiAutomation.WalkHierarchyAsync(handle.Value, maxElements, ct);
                    if (winResult.IsSuccess)
                    {
                        var snapshot = winResult.Value;
                        elementProcessId = snapshot.ProcessId;

                        if (elementProcessId == _recorderProcessId)
                            return;

                        windowId = snapshot.WindowId;
                        targetElement = snapshot.RootElement;
                        applicationTag = snapshot.ApplicationTag;
                        await ApplyWindowSnapshotAsync(snapshot, settings);
                    }
                }
            }

            if (elementProcessId == _recorderProcessId)
                return;

            if (targetElement != null && windowId == Guid.Empty)
            {
                var owningResult = await _uiAutomation.GetOwningWindowAsync(targetElement, ct);
                if (owningResult.IsSuccess)
                {
                    var snapshot = owningResult.Value;
                    if (snapshot.ProcessId == _recorderProcessId)
                        return;

                    windowId = snapshot.WindowId;
                    applicationTag = snapshot.ApplicationTag;
                    await ApplyWindowSnapshotAsync(snapshot, settings);
                }
            }

            if (string.IsNullOrEmpty(applicationTag) || applicationTag == "Unknown")
            {
                applicationTag = ResolveDefaultApplicationTag();
            }

            var seq = Interlocked.Increment(ref _sequenceNumber);
            var action = ActionCoalescingPolicy.Coalesce(
                events.AsReadOnly(),
                targetElement ?? new ElementInfo("unknown", null, null, "Unknown", null, null, null, null,
                    false, false, false, new BoundingRectangle(0, 0, 0, 0), [], null, 0, 0, []),
                windowId,
                applicationTag,
                seq);

            var screenshotMode = settings?.ScreenshotMode ?? ScreenshotMode.EveryAction;
            if (screenshotMode == ScreenshotMode.EveryAction ||
                (screenshotMode == ScreenshotMode.WindowChangeOnly && action.ActionType == ActionType.WindowActivated))
            {
                var workingFolder = GetSessionScreenshotsFolder();

                Result<ScreenshotReference> screenshotResult;

                if (windowId != Guid.Empty && _session.Windows.TryGetValue(windowId, out var winSnap))
                {
                    screenshotResult = await _screenshotCapturer.CaptureWindowAsync(
                        winSnap.NativeWindowHandle, workingFolder, ct);
                }
                else
                {
                    screenshotResult = await _screenshotCapturer.CaptureFullScreenAsync(workingFolder, ct);
                }

                if (screenshotResult.IsSuccess)
                {
                    var screenshotRef = screenshotResult.Value with { AssociatedActionId = action.ActionId };
                    action = action with { ScreenshotId = screenshotRef.ScreenshotId };
                    lock (_lock)
                    {
                        _session!.Screenshots.Add(screenshotRef);
                    }
                }
                else
                {
                    _logger.LogWarning("Screenshot capture failed for action {Seq}: {Reason}",
                        action.SequenceNumber, screenshotResult.ErrorMessage);
                }
            }

            lock (_lock)
            {
                _session.Actions.Add(action);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush coalesced action");
        }
    }

    private async Task HeartbeatMonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);

                var elapsed = (DateTime.UtcNow - _inputHook.LastHeartbeatUtc).TotalSeconds;
                if (elapsed > HeartbeatThresholdSeconds && CurrentState == RecordingSessionState.Recording)
                {
                    _logger.LogWarning("No input detected for {Elapsed:F0}s (hook heartbeat stale)", elapsed);
                    if (elapsed > HeartbeatThresholdSeconds * 3)
                    {
                        ErrorOccurred?.Invoke(
                            "No input detected for a while. If you are actively using the target application, " +
                            "try clicking on it to ensure it has focus, or pause and resume the session.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ApplyWindowSnapshotAsync(WindowSnapshot snapshot, Settings? settings)
    {
        if (snapshot.ProcessId == _recorderProcessId)
        {
            _logger.LogDebug("Skipping Recorder's own window: {Title}", snapshot.Title);
            return;
        }

        var minInterval = GetMinimumRecaptureInterval(
            settings?.HierarchyRecaptureSensitivity ?? HierarchyRecaptureSensitivity.Medium);

        lock (_lock)
        {
            if (_session == null) return;

            var handle = snapshot.NativeWindowHandle;

            if (_windowHandleMap.TryGetValue(handle, out var existingId))
            {
                var resolvedId = existingId;

                if (!_session.Windows.TryGetValue(resolvedId, out var existing))
                {
                    _windowHandleMap.TryRemove(handle, out _);
                    _session.Windows[resolvedId] = snapshot with { WindowId = resolvedId };
                    return;
                }

                var timeSinceLastCapture = DateTime.UtcNow - existing.LastUpdatedAtUtc;
                var shouldRecapture = HierarchyRecapturePolicy.ShouldRecapture(
                    existing.StructuralFingerprint,
                    snapshot.StructuralFingerprint,
                    minInterval,
                    timeSinceLastCapture);

                if (shouldRecapture)
                {
                    var updated = snapshot with
                    {
                        WindowId = resolvedId,
                        FirstCapturedAtUtc = existing.FirstCapturedAtUtc,
                        CaptureCount = existing.CaptureCount + 1
                    };
                    _session.Windows[resolvedId] = updated;
                    _logger.LogDebug("Window re-captured: {Title} (capture #{Count})",
                        snapshot.Title, updated.CaptureCount);
                }
            }
            else
            {
                _session.Windows[snapshot.WindowId] = snapshot;
                _windowHandleMap[handle] = snapshot.WindowId;
            }
        }
    }

    private static TimeSpan GetMinimumRecaptureInterval(HierarchyRecaptureSensitivity sensitivity)
        => sensitivity switch
        {
            HierarchyRecaptureSensitivity.Low => TimeSpan.FromSeconds(2),
            HierarchyRecaptureSensitivity.Medium => TimeSpan.FromMilliseconds(500),
            HierarchyRecaptureSensitivity.High => TimeSpan.FromMilliseconds(100),
            _ => TimeSpan.FromMilliseconds(500)
        };

    private string GetSessionScreenshotsFolder()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUiFlowRecorder", "Sessions",
            _session?.SessionId.ToString() ?? "unknown");
        return Path.Combine(baseDir, "screenshots");
    }

    private int GetMaxElementCount(Settings? settings)
        => settings?.MaxHierarchyElementCount ?? 5000;

    private string ResolveDefaultApplicationTag()
    {
        lock (_lock)
        {
            if (_session == null || _session.TargetApplicationContexts.Count == 0)
                return "TargetApp";

            var active = _session.TargetApplicationContexts.FirstOrDefault(c => c.IsActive);
            return active?.ApplicationTag
                ?? _session.TargetApplicationContexts[0].ApplicationTag;
        }
    }

    private void SetState(RecordingSessionState newState)
    {
        CurrentState = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        (_inputHook as IDisposable)?.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
}