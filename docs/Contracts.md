# Contracts — Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Defines all interface method signatures, result types, value objects, event types, and enum contracts that the architecture documents (`Architecture.md`, `SystemDesign.md`) reference conceptually but leave unspecified. This is the single reference for every method signature an implementing engineer or coding agent must produce in Phase 1 (`Roadmap.md`). No file in this document corresponds to source code — the types named here are the actual C# types to create.

Every interface, type, and method below carries its owning project and namespace as documented in `Architecture.md` §4 and `CodingGuidelines.md` §15.

---

## 1. Result & Error Types

Namespace: `WindowsUiFlowRecorder.Domain.Common`

### `Result` (non-generic, void-returning operations)

```csharp
public readonly struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public FailureReason? FailureReason { get; }
    public string? ErrorMessage { get; }  // Human-readable, caller-facing

    public static Result Success();
    public static Result Failure(FailureReason reason, string? message = null);

    public void Deconstruct(out bool isSuccess, out FailureReason? reason);
}
```

### `Result<T>` (value-returning operations)

```csharp
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T Value { get; }              // Valid only when IsSuccess
    public FailureReason? FailureReason { get; }
    public string? ErrorMessage { get; }

    public static Result<T> Success(T value);
    public static Result<T> Failure(FailureReason reason, string? message = null);
}
```

### `FailureReason` enum

```csharp
public enum FailureReason
{
    // Launch-chain failures
    ProcessNotStarted,
    ReadinessTimeout,
    ProcessCrashedBeforeReady,

    // UIA correlation failures
    ElementNotFound,
    ElementStale,
    WindowNotFound,
    HierarchyTooDeep,

    // Export / persistence failures
    ExportValidationFailed,
    DiskWriteFailed,
    SerializationFailed,

    // Data / configuration errors
    InvalidProfile,
    InvalidLaunchChain,
    ConditionMisconfigured,
    SessionNotFound,

    // Infrastructure errors
    ElevationMismatch,          // R-03: target elevated, recorder not
    InputHookDisconnected,      // OS disabled the hook
    AutomationNotAvailable,

    // General
    OperationCanceled,
    Unknown
}
```

### `DomainException` base class

```csharp
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}
```

---

## 2. Value Objects

Namespace: `WindowsUiFlowRecorder.Domain.Common`

### ScreenPoint

```csharp
public readonly record struct ScreenPoint(int X, int Y);
```

### BoundingRectangle

```csharp
public readonly record struct BoundingRectangle(int X, int Y, int Width, int Height);
```

### StructuralFingerprint

```csharp
// Opaque string produced by HierarchyRecapturePolicy.
// Algorithm: hash of (child count per container, ordered ControlTypes/AutomationIds per level).
public readonly record struct StructuralFingerprint(string Value);
```

---

## 3. Domain Entity Contracts

These are records/classes with init-only properties matching `DataModel.md` §5 exactly. Only their names and defining project are listed here; the full field lists are in `DataModel.md` and are authoritative.

Namespace: `WindowsUiFlowRecorder.Domain.Entities`

- `RecordingSession` (aggregate root, mutable `State`)
- `TargetApplicationContext` (runtime handle, mutable `IsActive`)
- `RecordedAction` (immutable)
- `WindowSnapshot` (immutable, carries `StructuralFingerprint`)
- `ElementInfo` (immutable, recursive via `Children`)
- `ScreenshotReference` (immutable)
- `ApplicationLaunchChain` (immutable, holds ordered `LaunchStep[]`)
- `LaunchStep` (immutable)
- `ReadinessCondition` (immutable)
- `ApplicationProfile` (immutable)
- `Settings` (immutable)
- `SessionListItem` (immutable projection)

---

## 4. Enum Contracts

Namespace: `WindowsUiFlowRecorder.Domain.Common` unless otherwise noted.

### `RecordingSessionState`

```csharp
public enum RecordingSessionState
{
    Idle,
    Configuring,
    LaunchingChain,
    Recording,
    Paused,
    Stopped,
    Reviewing,
    Exporting,
    Exported,
    LaunchFailed
}
```

### `ActionType`

```csharp
public enum ActionType
{
    Click,
    RightClick,
    DoubleClick,
    Drag,
    TextEntry,
    KeyPress,
    FocusChanged,
    WindowActivated
}
```

### `TerminationReason`

```csharp
public enum TerminationReason
{
    UserStopped,
    AllTargetsCrashedOrExited,
    Unknown
}
```

### `TargetTerminationReason`

```csharp
public enum TargetTerminationReason
{
    ProcessCrashed,
    ProcessExitedNormally,
    NotTerminated
}
```

### `ConditionType`

```csharp
public enum ConditionType
{
    ProcessStarted,
    WindowAppeared,
    ControlPresent,
    ControlPropertyEquals,
    FixedTimeout
}
```

### `WindowMatchMode`

```csharp
public enum WindowMatchMode
{
    Exact,
    Contains,
    Regex
}
```

### `PropertyMatchMode`

```csharp
public enum PropertyMatchMode
{
    Exact,
    Contains,
    Regex
}
```

### `ExpectedPropertyName`

```csharp
public enum ExpectedPropertyName
{
    Name,
    Value,
    Text
}
```

### `ExportKind`

```csharp
public enum ExportKind
{
    RecordingSession,
    StandaloneScan
}
```

### `ScreenshotMode`

```csharp
public enum ScreenshotMode
{
    EveryAction,
    WindowChangeOnly,
    ManualCheckpointOnly,
    Off
}
```

### `ScreenshotScope`

```csharp
public enum ScreenshotScope
{
    FullScreen,
    Window,
    Element
}
```

### `ScreenshotFormat`

```csharp
public enum ScreenshotFormat
{
    PNG
}
```

### `HierarchyRecaptureSensitivity`

```csharp
public enum HierarchyRecaptureSensitivity
{
    Low,     // Longer minimum interval (e.g. 2000ms)
    Medium,  // Default interval (e.g. 500ms)
    High     // Shorter interval (e.g. 100ms)
}
```

### `InputEventType`

```csharp
public enum InputEventType
{
    MouseDown,
    MouseUp,
    MouseMove,
    MouseWheel,
    KeyDown,
    KeyUp,
    FocusGained,
    FocusLost,
    WindowActivated
}
```

---

## 5. Raw Input Event Contract

Namespace: `WindowsUiFlowRecorder.Application.Abstractions`

```csharp
// Produced by IGlobalInputHook, consumed by RecordingSessionService.
// Represents one raw OS-level input event. Carries all fields a
// coalescing/correlation step needs without exposing Win32 types.
public readonly record struct RawInputEvent(
    InputEventType EventType,
    DateTime TimestampUtc,
    ScreenPoint? ScreenPosition,    // null for keyboard-only events
    int? VirtualKeyCode,            // null for mouse-only events
    bool IsPrintableKey,            // true if VirtualKeyCode maps to printable text
    IntPtr? WindowHandle            // The window that had focus or received the event, if known
);
```

---

## 6. Application Service Interfaces

Defined in `Application`, implemented in the same layer's service classes. ViewModels depend on these interfaces exclusively.

Namespace: `WindowsUiFlowRecorder.Application.Recording`

### `IRecordingSessionService`

```csharp
public interface IRecordingSessionService
{
    Task<Result> StartSessionAsync(
        ApplicationLaunchChain launchChain,
        IReadOnlyList<TargetApplicationContext> contexts,
        CancellationToken ct);

    Result PauseSession();
    Result ResumeSession();
    Task<Result<RecordingSession>> StopSessionAsync();

    RecordingSessionState CurrentState { get; }
    event Action<RecordingSessionState>? StateChanged;

    // Thread-safe access to the in-memory aggregate for UI display.
    SessionListItem? GetSessionSummary();
}
```

Namespace: `WindowsUiFlowRecorder.Application.Launching`

### `IApplicationLaunchOrchestrator`

```csharp
public interface IApplicationLaunchOrchestrator
{
    // Executes the full launch chain: launches Step 1, polls its
    // ReadinessCondition, proceeds to Step 2..N, etc.
    // Returns the list of TargetApplicationContexts on success.
    // Cleans up already-started processes on failure if CleanUpOnFailure is set.
    Task<Result<IReadOnlyList<TargetApplicationContext>>> ExecuteLaunchChainAsync(
        ApplicationLaunchChain chain,
        int pollIntervalMs,
        CancellationToken ct);
}
```

Namespace: `WindowsUiFlowRecorder.Application.Scanning`

### `IUiScanService`

```csharp
public interface IUiScanService
{
    // Performs an on-demand full hierarchy scan of the given window.
    // Independent of any recording session.
    Task<Result<WindowSnapshot>> ScanWindowAsync(
        IntPtr windowHandle,
        int maxElementCount,
        CancellationToken ct);
}
```

Namespace: `WindowsUiFlowRecorder.Application.Export`

### `IExportService`

```csharp
public interface IExportService
{
    // Map an internal RecordingSession to an ExportPackage and write it.
    Task<Result> ExportSessionAsync(
        RecordingSession session,
        string outputDirectory,
        CancellationToken ct);

    // Map a standalone scan (WindowSnapshot) to an ExportPackage and write it.
    Task<Result> ExportStandaloneScanAsync(
        WindowSnapshot snapshot,
        string outputDirectory,
        CancellationToken ct);
}
```

Namespace: `WindowsUiFlowRecorder.Application.Profiles`

### `IApplicationProfileService`

```csharp
public interface IApplicationProfileService
{
    Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync();
    Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId);
    Task<Result> SaveProfileAsync(ApplicationProfile profile);
    Task<Result> DeleteProfileAsync(Guid profileId);
    Task<Result<ApplicationProfile>> DuplicateProfileAsync(Guid profileId, string newName);
}
```

Namespace: `WindowsUiFlowRecorder.Application.Settings`

### `ISettingsService`

```csharp
public interface ISettingsService
{
    Task<Result<Settings>> GetSettingsAsync();
    Task<Result> UpdateSettingsAsync(Settings settings);
}
```

---

## 7. Infrastructure-Facing Interfaces (owned by Application)

Defined in `Application`, implemented in `Infrastructure`. These are the seams that keep Domain/Application ignorant of FlaUI, Win32, and file I/O.

Namespace: `WindowsUiFlowRecorder.Application.Abstractions`

### `IUiAutomationProvider`

```csharp
public interface IUiAutomationProvider
{
    // Lookup an element by screen point (for mouse clicks).
    Task<Result<ElementInfo>> GetElementAtPointAsync(
        ScreenPoint point,
        CancellationToken ct);

    // Lookup the currently focused element (for keyboard/focus events).
    Task<Result<ElementInfo>> GetFocusedElementAsync(CancellationToken ct);

    // Full depth-first hierarchy walk of a window.
    // Respects maxElementCount safety limit; truncates with a logged warning if exceeded.
    Task<Result<WindowSnapshot>> WalkHierarchyAsync(
        IntPtr windowHandle,
        int maxElementCount,
        CancellationToken ct);

    // Subscribe to focus-changed and window-activated UIA events
    // for the given contexts. Callbacks are raised on a background thread.
    Task<Result> SubscribeToEventsAsync(
        IReadOnlyList<TargetApplicationContext> contexts,
        Action<ElementInfo> onFocusChanged,
        Action<WindowSnapshot> onWindowActivated,
        Action<IntPtr> onWindowOpened,
        CancellationToken ct);

    // Unsubscribe all event listeners (on session stop).
    Task UnsubscribeAllAsync();

    // Resolve an ElementInfo's owning window and process context.
    // Uses the element's current runtime handle (re-resolved from ElementId).
    Task<Result<WindowSnapshot>> GetOwningWindowAsync(
        ElementInfo element,
        CancellationToken ct);
}
```

### `IProcessLaunchMonitor`

```csharp
public interface IProcessLaunchMonitor
{
    // Launch a process from a LaunchStep configuration.
    // Returns the process id on success.
    Task<Result<int>> StartProcessAsync(
        string executablePath,
        string? arguments,
        string? workingDirectory,
        CancellationToken ct);

    // Check if a process is still running.
    Task<bool> IsProcessRunningAsync(int processId);

    // Enumerate visible top-level windows for a process.
    // Returns window handles; empty if none visible.
    Task<Result<IReadOnlyList<IntPtr>>> EnumerateTopLevelWindowsAsync(int processId);

    // Subscribe to process exit events for the given process ids.
    // callback is invoked on a background thread with the pid that exited.
    Task SubscribeToExitEventsAsync(
        IReadOnlyList<int> processIds,
        Action<int> onProcessExited);

    // Unsubscribe from all process exit event subscriptions.
    Task UnsubscribeAllAsync();

    // Kill a process (used during CleanUpOnFailure).
    Task KillProcessAsync(int processId);
}
```

### `IScreenshotCapturer`

```csharp
public interface IScreenshotCapturer
{
    // Capture the full virtual screen.
    Task<Result<ScreenshotReference>> CaptureFullScreenAsync(
        string workingFolder,
        CancellationToken ct);

    // Capture a specific window's bounding rectangle.
    Task<Result<ScreenshotReference>> CaptureWindowAsync(
        IntPtr windowHandle,
        string workingFolder,
        CancellationToken ct);

    // Capture an element's bounding rectangle (FR-5.3).
    Task<Result<ScreenshotReference>> CaptureElementAsync(
        BoundingRectangle elementBounds,
        string workingFolder,
        CancellationToken ct);
}
```

### `IGlobalInputHook`

```csharp
public interface IGlobalInputHook
{
    // Subscribe to receive raw input events.
    // callback is invoked from the hook's dedicated thread.
    Task SubscribeAsync(Action<RawInputEvent> onInputEvent, CancellationToken ct);

    // Unsubscribe and unhook.
    Task UnsubscribeAsync();

    // Last heartbeat timestamp. If more than the configured threshold
    // (default 5s) elapses without a heartbeat, the hook is presumed
    // disabled by the OS and the overlay should display a warning.
    DateTime LastHeartbeatUtc { get; }
}
```

### `IExportWriter`

```csharp
public interface IExportWriter
{
    // Write the final ExportPackage and copy screenshot files.
    // All paths inside the export are rewritten as relative (FR-7.3).
    Task<Result> WriteExportAsync(
        ExportPackage exportPackage,
        string outputDirectory,
        IReadOnlyList<ScreenshotReference> screenshots,
        CancellationToken ct);
}
```

---

## 8. Repository Interfaces (owned by Domain)

Defined in `Domain`, implemented in `Infrastructure`.

Namespace: `WindowsUiFlowRecorder.Domain.Abstractions`

### `ISessionRepository`

```csharp
public interface ISessionRepository
{
    Task<Result> SaveSessionAsync(RecordingSession session);
    Task<Result<RecordingSession>> LoadSessionAsync(Guid sessionId);
    Task<Result<IReadOnlyList<SessionListItem>>> ListSessionsAsync();
    Task<Result> DeleteSessionAsync(Guid sessionId);
    Task<Result> UpdateSessionMetadataAsync(Guid sessionId, string? name, string? note);
}
```

### `IApplicationProfileRepository`

```csharp
public interface IApplicationProfileRepository
{
    Task<Result<IReadOnlyList<ApplicationProfile>>> GetAllProfilesAsync();
    Task<Result<ApplicationProfile>> GetProfileAsync(Guid profileId);
    Task<Result> SaveProfileAsync(ApplicationProfile profile);
    Task<Result> DeleteProfileAsync(Guid profileId);
}
```

### `ISettingsRepository`

```csharp
public interface ISettingsRepository
{
    Task<Result<Settings>> LoadSettingsAsync();
    Task<Result> SaveSettingsAsync(Settings settings);
}
```

---

## 9. Domain Policy Contracts

Pure stateless classes in `Domain.Policies`. No injected dependencies, no async, no I/O.

Namespace: `WindowsUiFlowRecorder.Domain.Policies`

### `ActionCoalescingPolicy`

```csharp
public static class ActionCoalescingPolicy
{
    // Given a buffer of raw input events for the same control/focus context,
    // returns the coalesced RecordedAction.
    //   - Drag: mouse-down + move(s) + mouse-up within 2s -> single Drag or Click.
    //   - TextEntry: printable keys to same control, gap < 1.5s, no focus change.
    //   - KeyPress: non-printable key as single action.
    //   - WindowActivated: collapses duplicates for the same WindowId.
    public static RecordedAction Coalesce(IReadOnlyList<RawInputEvent> events, ElementInfo targetElement);
}
```

### `HierarchyRecapturePolicy`

```csharp
public static class HierarchyRecapturePolicy
{
    // Decide whether a re-capture is warranted given the previous and current
    // fingerprints, the minimum re-capture interval, and time since last capture.
    public static bool ShouldRecapture(
        StructuralFingerprint previousFingerprint,
        StructuralFingerprint currentFingerprint,
        TimeSpan minimumInterval,
        TimeSpan timeSinceLastCapture);

    // Compute a structural fingerprint from a WindowSnapshot for later comparison.
    public static StructuralFingerprint ComputeFingerprint(WindowSnapshot snapshot);
}
```

---

## 10. DI Registration Contracts

Namespace: `WindowsUiFlowRecorder.Infrastructure.DependencyInjection` (per project)

```csharp
// Order of registration (called from App.xaml.cs):
//   services.AddDomainLayer()
//           .AddApplicationLayer()
//           .AddInfrastructureLayer()
//           .AddPresentationLayer();

public static class DomainLayerRegistration
{
    public static IServiceCollection AddDomainLayer(this IServiceCollection services);
}

public static class ApplicationLayerRegistration
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services);
}

public static class InfrastructureLayerRegistration
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services);
}

public static class PresentationLayerRegistration
{
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services);
}
```

Default lifetimes (from `CodingGuidelines.md` §6):
| Type | Lifetime | Rationale |
|---|---|---|
| `IRecordingSessionService` | Scoped | Holds session state; one per active session |
| `IApplicationLaunchOrchestrator` | Singleton | Stateless; no per-session state |
| `IUiScanService` | Singleton | Stateless |
| `IExportService` | Singleton | Stateless |
| `IApplicationProfileService` | Singleton | Stateless, delegates to repo |
| `ISettingsService` | Singleton | Stateless, delegates to repo |
| `IUiAutomationProvider` | Singleton | Wraps single native UIA3Automation instance |
| `IProcessLaunchMonitor` | Singleton | Tracks all launched processes |
| `IScreenshotCapturer` | Singleton | Manages async write queue |
| `IGlobalInputHook` | Singleton | One OS-level hook per process |
| All repository interfaces | Singleton | Backed by local file system |
| `IExportWriter` | Singleton | Stateless |
