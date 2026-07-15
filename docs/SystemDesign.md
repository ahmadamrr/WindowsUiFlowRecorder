# System Design Document
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** This document sits between `Architecture.md` (structure/layers/contracts) and `DataModel.md` (field-level schemas). It defines *runtime behavior*: state machines, algorithms, threading, timing budgets, file layout, and failure handling — detailed enough that an implementing engineer or coding agent does not need to make further design decisions. It must not introduce any component, interface, or entity not already named in `Architecture.md`. No source code is included.

---

## 1. Purpose & Scope

`Architecture.md` defines *what* the layers and components are and how they depend on each other. This document defines *how they behave at runtime*: what happens, in what order, on what thread, with what timing, and what happens when something fails. It is grounded throughout in the reference scenario from the PRD: a **Proxy App** that must reach an HSM-connected state before the **eAdmin App** is launched — used here as the concrete worked example for the generic `ApplicationLaunchChain` design.

---

## 2. Runtime Topology

At runtime there are always at least two, and typically three, separate OS processes involved:

| Process | Owned by | Notes |
|---|---|---|
| **Recorder Process** | This application (`WindowsUiFlowRecorder.Presentation.exe`) | .NET 8 process; hosts WPF UI thread, capture pipeline, UIA3 automation client, DI container. |
| **Proxy App Process** | Target under test (Primary) | Independent process, any Windows UI technology/.NET runtime (e.g., .NET Framework 4.8). The Recorder only talks to it via UI Automation (out-of-process COM) and OS process APIs — never in-process. |
| **eAdmin App Process** | Target under test (Dependent) | Same as above; launched only after the Proxy App's `ReadinessCondition` is satisfied. |

The Recorder process never loads code into, or injects into, a target process. All interaction is through: (a) the UI Automation provider (out-of-process, via FlaUI/UIA3), (b) `Process.Start`/process monitoring APIs, and (c) OS-level input hooking (which observes global input, not target-process internals). This is what allows the Recorder to remain .NET 8 while targets run .NET Framework 4.8 or any other Windows UI stack.

---

## 3. Recording Session State Machine

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Configuring: Tester opens Target Application Selection
    Configuring --> LaunchingChain: Tester clicks "Start Recording"
    LaunchingChain --> Recording: All launch chain steps + readiness conditions succeeded
    LaunchingChain --> LaunchFailed: A step's ReadinessCondition times out
    LaunchFailed --> Configuring: Tester adjusts profile / retries
    Recording --> Paused: Tester clicks "Pause"
    Paused --> Recording: Tester clicks "Resume"
    Recording --> Stopped: Tester clicks "Stop"
    Paused --> Stopped: Tester clicks "Stop"
    Recording --> Stopped: Target application(s) crash/exit unexpectedly (auto-stop, session preserved)
    Stopped --> Reviewing: Session summary displayed
    Reviewing --> Exporting: Tester clicks "Export"
    Exporting --> Exported: Export written and validated
    Exported --> Idle: Tester starts a new session
    Exported --> Reviewing: Tester re-opens session from Session List for re-export
```

**Rules:**
- A session can only transition `LaunchingChain → Recording` after *every* step of the `ApplicationLaunchChain` has satisfied its `ReadinessCondition` (see §4). Partial success is not a valid state — the session either fully starts or aborts to `LaunchFailed`.
- `Recording → Stopped` triggered by an unexpected target crash must preserve every `RecordedAction`/`WindowSnapshot` captured up to that point (NFR "Reliability" in PRD.md) and must record the crash as a terminal event on the affected `TargetApplicationContext`, not silently truncate the session.
- `Exported` sessions remain re-exportable indefinitely (FR-7.4) without re-entering `Recording`.

---

## 4. Application Launch Chain Execution Design

This is the runtime algorithm behind `ApplicationLaunchOrchestrator` (owned by the Application layer, per Architecture.md §3.2), worked through using the Proxy App → eAdmin App scenario.

### 4.1 Execution algorithm

1. Load the `ApplicationLaunchChain` from the selected `ApplicationProfile` (Step 1 = Primary = Proxy App; Step 2..N = Dependent = eAdmin App and any further steps).
2. For the current step:
   a. Start the process via `IProcessLaunchMonitor.StartProcess(path, arguments, workingDirectory)`.
   b. If the process fails to start at the OS level (bad path, access denied), abort immediately with a `LaunchChainResult.Failure` naming this step and the OS error — do not enter a readiness-polling loop for a process that never started.
   c. Begin polling the step's `ReadinessCondition` (see §5) at a fixed poll interval (default 250ms, configurable) until either the condition is satisfied or the step's timeout elapses (default 30s, configurable per PRD FR-9.1's "default readiness-condition timeout").
   d. If satisfied: record the `TargetApplicationContext` for this step (process id, application tag, e.g. `"ProxyApp"`) and proceed to the next step.
   e. If timeout elapses first: abort the entire chain (do not proceed to launch remaining steps), terminate any processes already started by this chain execution **only if** the profile is configured to "clean up on failure" (default: on — configurable, since a tester may want the partially-launched app left open to diagnose), and return `LaunchChainResult.Failure` naming the failed step and the unmet condition.
3. Once all steps succeed, return `LaunchChainResult.Success` with the full ordered list of `TargetApplicationContext`s. `RecordingSessionService` then subscribes input/UIA listeners to all of them (§6, §7) before transitioning to `Recording`.

### 4.2 Concrete worked example (Proxy App / eAdmin App / HSM)

| Step | Target | Readiness Condition | Typical evaluation |
|---|---|---|---|
| 1 (Primary) | Proxy App | Control-property-equals: a named status control's text equals `"Connected"` (or matches a configured pattern) | Poll: find element by AutomationId/Name within the Proxy App's main window; compare its Name/Value property each poll cycle |
| 2 (Dependent) | eAdmin App | Process-started + window-appeared (eAdmin's own main window becomes visible) | Poll: process exists AND has ≥1 visible top-level window |

This is not hardcoded — the same engine supports any N-step chain with any mix of condition types per step; Proxy/eAdmin/HSM is simply the profile a tester configures using this generic mechanism.

### 4.3 Sequence (detail beyond Architecture.md §7.1)

```mermaid
sequenceDiagram
    participant Orch as ApplicationLaunchOrchestrator
    participant Proc as IProcessLaunchMonitor
    participant Uia as IUiAutomationProvider
    participant Timer as Poll Timer

    Orch->>Proc: StartProcess(ProxyApp)
    Proc-->>Orch: pid
    Orch->>Timer: begin polling (interval=250ms, timeout=30s)
    loop every 250ms until satisfied or timeout
        Timer->>Uia: EvaluateCondition(ProxyApp, condition)
        Uia-->>Timer: Satisfied=false
    end
    Uia-->>Timer: Satisfied=true (status = "Connected")
    Timer-->>Orch: ConditionMet
    Orch->>Proc: StartProcess(eAdminApp)
    Proc-->>Orch: pid
    Orch->>Timer: begin polling (window-appeared condition)
    Timer-->>Orch: ConditionMet
    Orch-->>Orch: Build TargetApplicationContext list [ProxyApp, eAdminApp]
```

---

## 5. Readiness Condition Evaluation Design

Each `ReadinessCondition` type (Architecture.md §10) is evaluated as follows:

| Condition type | Evaluation logic | Failure mode handled |
|---|---|---|
| Process-started | `Process` exists and has not exited | Process exits immediately after start (crash-on-launch) → immediate failure, no need to wait for timeout |
| Window-appeared | Enumerate top-level windows owned by the process; match by title (exact/contains/regex, configurable) or by root AutomationId | Window created but immediately closed (splash screen) → poll must re-check, not cache a stale positive |
| Control-present | `FindFirst`/`FindAll` scoped to the target window, matched by AutomationId, Name, and/or ControlType | Control exists but is not yet in a stable state (e.g., still constructing) → this condition only checks *presence*, not value; use control-property-equals when a specific state matters |
| Control-property-equals | Same lookup as control-present, then compare `Name`/`Value`/text-pattern content against an expected literal or pattern (exact match or contains, configurable) | Property read throws (element went stale between poll cycles) → treated as "not yet satisfied," not as a fatal error, and polling continues |
| Fixed-timeout (fallback) | Waits a fixed duration then reports satisfied unconditionally | Documented in PRD as a fallback only; using it means the orchestrator cannot distinguish "ready" from "not ready," so it is discouraged for anything but throwaway/manual profiles |

**Polling discipline:** all polling is time-boxed by the step timeout; the orchestrator never polls indefinitely. Each poll cycle that throws an unexpected (non-"element not found/stale") exception is logged and counted; if a step accumulates more than a configurable number of unexpected exceptions (default 5) before its timeout, the step fails early with that error surfaced, rather than waiting out the full timeout on a condition that is structurally broken (e.g., wrong AutomationId configured).

---

## 6. Input Capture Subsystem Design

- Implemented by `GlobalInputHook` (Infrastructure), consumed by `RecordingSessionService` (Application) via `IGlobalInputHook`.
- Uses OS-level low-level global mouse and keyboard hooks scoped to the whole desktop session (not per-process), because the Recorder must observe input across every window in the launch chain (Proxy App, eAdmin App, and any child dialogs) without cooperation from those target processes.
- Runs its own dedicated message-pump thread (a hook of this kind requires a thread with a live Windows message loop); this thread is never the WPF UI thread and never the UIA polling thread, to avoid blocking either.
- Raw events are pushed onto a thread-safe, bounded producer/consumer queue; `RecordingSessionService` drains the queue on a dedicated capture-processing thread, so a slow UIA lookup or screenshot write never delays the OS hook callback (a slow hook callback can cause the OS to silently disable the hook).
- Only events relevant to FR-3.1 (clicks, key presses, text entry, focus/window change) are retained; raw mouse-move events not part of a drag/gesture are discarded at the hook boundary and never reach the coalescing stage.

---

## 7. UI Automation Correlation Design

- On each retained input event, `RecordingSessionService` asks `IUiAutomationProvider` for the `AutomationElement` at the event's screen point (for clicks) or the currently focused element (for keyboard input and focus-change events).
- Element lookups are matched against the set of active `TargetApplicationContext`s by process id; an event whose element belongs to a process outside the current launch chain (e.g., the tester briefly alt-tabs to an unrelated app) is recorded as a window-deactivation boundary but is **not** added as a `RecordedAction`, per FR-3's intent that captured actions are scoped to the applications under test.
- Focus-changed and window-activated UIA events are subscribed per `TargetApplicationContext` at session start (Architecture.md §7.1) so that a window opened *after* recording starts (e.g., a modal dialog spawned by eAdmin App) is automatically discovered without the tester needing to reconfigure anything.
- Element references from UIA are inherently short-lived/can go stale; every correlation step re-resolves fresh metadata into the plain `ElementInfo` data shape at the moment of the action rather than holding onto a live `AutomationElement` reference, so nothing in the capture pipeline depends on stale native handles surviving past a single lookup.
- **Leaf-resolution requirement:** point-based lookup must resolve to the deepest/most specific element under the cursor, never stop early at an intermediate container or the window root. If the underlying UIA point-lookup API returns a container element (e.g., because the specific leaf control does not itself register as hit-testable), the correlation step must perform a bounded secondary search — walking the container's already-known descendants and selecting the one whose `BoundingRectangle` most tightly contains the event point — before falling back to the container. A `RecordedAction.TargetElement` with `ControlType: "Window"` or `ControlType: "Unknown"` is only ever acceptable when the tester's action genuinely targeted empty window chrome, not as a routine fallback for an unresolved click on a real control; if this outcome occurs at a materially higher rate than expected, it indicates a correlation defect, not a UIA limitation, and must be investigated rather than accepted as noise.
- **Ancestor path capture:** When resolving an element at a screen point, the correlation step also builds an ordered breadcrumb from the window root down to the resolved element. Each entry's format follows `DataModel.md` §4.5's `ElementPath` rule: `"ControlType"`, `"ControlType:Name"`, `"ControlType#AutomationId"`, or `"ControlType:Name#AutomationId"` depending on which properties the element exposes. This path is stored directly on the `RecordedAction.ElementPath` field and enables Page Object script generation without tree-walking the exported hierarchy.
- **Framework detection fallback:** The element's `FrameworkId` property is populated as follows, with each step only used if the previous one yielded an empty result:
  1. FlaUI's native `element.Properties.FrameworkId` (fastest, covers the majority of cases for modern UIA providers).
  2. Walk up the element's UIA parent chain — the ancestor (especially the window root) often exposes a `FrameworkId` that child elements under older frameworks (e.g. Win32/MFC) lack.
  3. If still empty, match the element's `ClassName` against known patterns: `"WindowsForms10*"` → `"WinForm"`, `"HwndWrapper*"` → check loaded modules.
  4. If unresolved, examine the target process's loaded modules for known framework DLLs: `"PresentationFramework.dll"` → `"WPF"`, `"System.Windows.Forms.dll"` → `"WinForm"`, `"Microsoft.UI.Xaml.dll"` → `"WinUI"`.
  5. Final fallback: `"Unknown"` — this is stored as a literal string, never as null or empty.

---

## 8. Action Coalescing Algorithm

Purpose: turn raw retained input events into meaningful discrete `RecordedAction`s (FR-3.4).

- **Drag/continuous gestures:** a mouse-down followed by mouse-move events followed by mouse-up on the same element (or a drag onto a different element) within a configurable maximum gesture duration (default 2s) is coalesced into a single action. **The click-vs-drag pixel threshold must use the operating system's own drag-initiation metrics (`SM_CXDRAG`/`SM_CYDRAG`, 4 pixels by default on stock Windows) rather than an arbitrary internal constant.** Any mouse-down/mouse-up pair whose total displacement stays within that OS-defined threshold — including ordinary sub-pixel jitter naturally present in real mouse input — must be classified as a `Click`/`RightClick`/`DoubleClick`, never a `Drag`. `Drag` is reserved for gestures whose displacement genuinely and clearly exceeds this threshold. Because this OS metric can vary by machine/DPI setting, it must be read at runtime via the platform API rather than hardcoded, and a session in which the overwhelming majority of actions classify as `Drag` should be treated as a signal of a threshold or displacement-measurement defect, not as expected tester behavior.
- **Text entry:** consecutive individual key-press events that produce printable text into the *same* focused control, with no intervening focus change and no gap longer than a configurable idle threshold (default 1.5s), are coalesced into a single "text entry" action carrying the final resulting text value of that control, rather than one action per keystroke. A non-printable key (Enter, Tab, Escape, function keys) always ends the current text-entry coalescing window and is itself recorded as its own discrete key-press action. **Each physical keystroke must be counted exactly once.** Windows text input typically delivers more than one low-level message per character (e.g., a key-down and a character message); the input hook (§6) must designate exactly one of these as the canonical source of truth for printable-character capture (character/text messages for the resulting printable text, key-down/key-up only for non-printable control keys) and must not treat both as independent keystrokes. An exported `EnteredText` value containing doubled or duplicated characters relative to what was actually typed is a defect in this deduplication, not a legitimate variation in coalescing behavior, and must be covered by a dedicated regression test (`TestingStrategy.md` §8) asserting character-for-character equality between a scripted input string and the resulting `EnteredText`.
- **Duplicate window-activation events:** rapid repeated "window activated" events for the same window (which the OS can sometimes fire more than once) are collapsed to a single `WindowSnapshot` touch rather than duplicated entries.
- Coalescing windows are evaluated on the capture-processing thread (§6) and never block the input-hook thread.

---

## 9. Window Hierarchy Capture & Re-capture Sensitivity Design

- **Window identity is keyed by the native OS window handle, never freshly generated per capture.** `WindowId` (`DataModel.md` §4.6/§5.3) must be a deterministic value derived from the target window's native handle (HWND) for the lifetime of that window — the *same* physical window, activated or re-captured any number of times during a session, must always resolve to the *same* `WindowId` and update the *same* stored `WindowSnapshot` in place. A fresh `WindowId`/fresh `WindowSnapshot` is only ever created when the underlying native window handle genuinely does not exist yet in the session's tracked set (a true first activation, or a genuinely new window such as a newly-opened dialog). **Generating a new `WindowId` on every activation or every recorded action — rather than looking up the existing entry for that handle — is a correctness defect**, not an acceptable side effect of re-capture: it silently multiplies the exported hierarchy size by the number of activations with zero added information, and is exactly the failure mode this rule exists to prevent. This must be covered by a dedicated integration test asserting that N activations of the same real window over a session produce exactly one `WindowInformation` entry, not N.
- **Initial capture:** the first time a window belonging to a tracked `TargetApplicationContext` is activated (i.e., its native handle is not yet present in the session's tracked window set), `IUiAutomationProvider` performs a depth-first walk of its descendant elements up to a configurable maximum depth (default: unlimited, but capped by a max-element-count safety limit, default 5,000, to bound worst-case scan time per NFR "Performance — hierarchy scan").
- **Change detection for re-capture (FR-4.2):** after the initial capture, the tool computes a lightweight structural fingerprint of the window (e.g., a hash derived from child count per container, the ordered list of ControlTypes/AutomationIds at each level) rather than a full content hash of every property, so that detecting "did the structure change" is cheap enough to check frequently.
- **Re-capture trigger:** a re-capture is scheduled when (a) a UIA structure-changed event fires for the window, or (b) the next recorded action targets that window (identified by native handle, per the identity rule above) and the fingerprint has changed since the last capture — whichever happens first — subject to a minimum re-capture interval (default 500ms, configurable via the "hierarchy re-capture sensitivity" setting in FR-9.1) to avoid thrashing on windows that mutate continuously (e.g., a live-updating status field).
- Re-captures replace the window's stored `WindowSnapshot` **for that same `WindowId`** in the session aggregate; prior captures of the same window are not retained as separate history entries in MVP (a single current snapshot per window, refreshed in place, keyed as above) — full versioned hierarchy history is noted as a candidate in FutureEnhancements.md.

---

## 10. Screenshot Capture Design

- Screenshots are captured on the capture-processing thread (never the hook thread), immediately after an action's `ElementInfo`/`WindowSnapshot` correlation completes (§7), per the mode configured in Settings (FR-5.1: every action / on window change only / on manual checkpoint / off).
- Capture targets, depending on settings: full virtual-screen bitmap, the owning window's bounding rectangle only, and/or (if FR-5.3 is enabled) a crop of just the target element's bounding rectangle.
- Images are encoded to PNG (lossless, since screenshots may be inspected closely for automation authoring) and written asynchronously to the session's working screenshot folder using a sequential, predictable naming convention (`{sessionId}/screenshots/{actionSequenceNumber}_{scope}.png`, where `{scope}` is `full`, `window`, or `element`), so that references stored on the in-progress `RecordedAction` are simple relative filenames resolvable at export time.
- Screenshot writes are queued and backpressured: if the write queue exceeds a safety threshold (indicating disk I/O is falling behind capture rate), the tool degrades gracefully by temporarily downgrading capture scope (e.g., window-only instead of full-screen) rather than blocking the tester's input, and logs a warning.

---

## 11. Concurrency & Threading Model

| Thread | Owns | Never does |
|---|---|---|
| WPF UI thread (Dispatcher) | All View/ViewModel updates, user interaction | Direct UIA calls, direct file I/O, direct process launch — always marshals to Application-layer services which internally hop to background threads |
| Input hook thread | OS-level global hook message pump | Any UIA lookup, any file write, any coalescing logic — only pushes raw events to the queue |
| Capture-processing thread | Draining input queue, coalescing, UIA correlation, triggering screenshot capture, appending to the in-memory `RecordingSession` aggregate | Blocking waits longer than the poll intervals defined in §4/§5 |
| Readiness-poll thread(s) (transient, only during `LaunchingChain`) | Polling `ReadinessCondition`s per §4/§5 | Persist past the launch phase; torn down once `Recording` begins or the chain aborts |
| Export/background I/O thread(s) | Writing screenshot files, writing the final export JSON, reading/writing local session/profile/settings JSON storage | Ever touch UI or raw input state directly — communicates results back to the UI thread via the Application-layer service's async result, marshalled through the Dispatcher |

Access to the shared in-memory `RecordingSession` aggregate during `Recording`/`Paused` is serialized through the capture-processing thread only (single-writer); read access for UI display (e.g., a live action counter) uses thread-safe snapshots/counters rather than direct aggregate access from the UI thread.

---

## 12. Storage & File Layout Design

Local, per-user application data root (no network, no shared/roaming assumptions beyond what Windows provides for the local profile):

```
%LOCALAPPDATA%\WindowsUiFlowRecorder\
├── Profiles\                     (one JSON file per saved ApplicationProfile / launch chain)
├── Sessions\
│   └── {sessionId}\
│       ├── session.json          (working/completed session data, pre-export internal format)
│       └── screenshots\          (working screenshot files, per §10 naming convention)
├── Settings\
│   └── settings.json
└── Logs\
    └── {date}.log                (local-only rolling log sink, per Architecture.md §9.2)
```

Export output (FR-7.1) is written to a **separate, user-chosen output directory**, distinct from the internal app-data root above, structured as a portable, self-contained package:

```
{user-chosen export folder}\
├── export.json                   (the ExportPackage root document, schema-versioned)
└── screenshots\                  (copied/relocated from the session's working folder;
                                    all references inside export.json are relative to
                                    this export folder root, per FR-7.3)
```

---

## 13. Performance & Resource Budget

Reference hardware baseline for all figures below (also referenced from PRD.md NFRs): a typical corporate QA workstation — quad-core x64 CPU, 16GB RAM, SSD storage, Windows 10/11, no virtualization overhead assumed beyond a standard corporate VM if applicable.

| Budget item | Target | Rationale |
|---|---|---|
| Added input latency per captured action | ≤ 100ms | Hook callback (§6) does no work beyond enqueue (~sub-ms); UIA lookup + coalescing + screenshot trigger happen off the hook thread so they cannot add to perceived input lag |
| Full hierarchy scan, ≤2,000 descendant elements | ≤ 3s | Bounds the Smart UI Scanner's on-demand scan (FR-6.1) and the Recorder's initial window capture (§9) |
| Readiness-condition poll interval | 250ms default | Frequent enough to detect readiness promptly without saturating the UIA client with lookups |
| Memory ceiling, 30-minute session, screenshots on | Documented, monitored ceiling (target order of magnitude: within normal desktop-app expectations, not unbounded growth) | Achieved primarily by (a) writing screenshots to disk immediately rather than buffering in memory (§10), and (b) storing a single current `WindowSnapshot` per window rather than full history (§9) |
| Export write + validation, typical session | Should not block the UI thread; runs on export/background I/O thread (§11) with a progress indicator for large sessions | Keeps the Reviewing→Exporting transition responsive |

---

## 14. Error Handling & Recovery Design

- **Target process crash during Recording:** `IProcessLaunchMonitor` detects process exit; `RecordingSessionService` marks the corresponding `TargetApplicationContext` as terminated, detaches its UIA subscriptions, and — if it was the *only* active context — transitions the session to `Stopped` automatically (state machine §3) rather than continuing to record against nothing. The session up to that point remains fully exportable (PRD NFR "Reliability").
- **UIA element/window goes stale mid-lookup:** treated as an expected, recoverable condition at the Infrastructure boundary (`FlaUiAutomationProvider`) — surfaced as "not found" rather than propagating a native COM exception up through Application/Domain.
- **Readiness condition never met:** handled per §4.1 step (e) — clean abort, named failing step, optional cleanup of already-started processes, no partial `Recording` state is ever entered.
- **Disk write failure during export:** `IExportWriter` reports a structured failure; the in-memory/working session data (§12) is left intact so the tester can retry export to a different location without re-recording (supports FR-7.4).
- **Global input hook silently disabled by the OS** (can happen if the hook callback thread is ever blocked): detected via a heartbeat check on the input-hook thread; if missed heartbeats exceed a threshold, the tool surfaces a visible warning on the Recording Overlay so the tester knows capture may be incomplete, rather than failing silently.

---

## 15. Configuration Schema Design (conceptual)

The exact JSON field names/types for `ApplicationProfile` and `ApplicationLaunchChain` are defined in `DataModel.md`. At the design level, a profile conceptually holds:

- Profile identity (name, description).
- An ordered list of launch steps; each step holds: executable path, launch arguments, working directory, an application tag (free-text label such as `"ProxyApp"`/`"eAdminApp"` used purely for display/export tagging, not for special-casing behavior), its `ReadinessCondition` (typed per §5), its readiness timeout override (falls back to the global default from Settings if unset), and its "clean up on failure" flag (§4.1 step (e)).
- This structure is what the Launch Chain Builder UI (Architecture.md §8) reads and writes; it is saved/loaded via `IApplicationProfileRepository`.

---

## 16. Security & Data Handling Design

- No captured data leaves local disk at any point (reinforces PRD NFR "Security/compliance" and Architecture.md §9.3's enforced no-network-access guarantee).
- Screenshots and captured `Name`/`Value` text necessarily reflect whatever is visibly rendered by the target application; the tool does not attempt to redact or filter this content, since doing so reliably is out of scope for MVP — this is documented as a residual risk in `RiskAnalysis.md`, with the mitigation being that testers control the export destination and are responsible for its handling per their environment's data-handling policy.
- Default-level application logs (§9.2 of Architecture.md) never include captured UI text content; only opt-in verbose diagnostic logging may, and that mode is clearly labeled in Settings so it is never accidentally left enabled.

---

## 17. Interaction Marking & Hierarchy Export Scope Pruning Design

This section defines the runtime behavior behind `DataModel.md` §4.7's `WasInteractedWith`/`InteractionCount`/`InteractedActionIds` fields and §7.1's `HierarchyExportScope` setting, added to address the "too many captured elements to know which ones matter" problem: with window-identity deduplication (§9) fixing the *quantity* of noise, this section fixes the *legibility* of what remains by marking which elements were actually touched and, optionally, pruning the export down to just those.

### 17.1 When interaction marking is computed
Marking is computed by `ExportService` at export time (`Architecture.md` §3.2), not maintained as live mutable state on the in-memory `WindowSnapshot` during recording. This keeps the capture pipeline (§6–§9) unchanged and free of additional bookkeeping, and guarantees the marking always reflects the final, fully-recaptured state of each window's tree at the moment of export — including windows that were recaptured (per §9) after the action that touched them was recorded.

### 17.2 Matching an action's target element to the final tree
Because a window's tree can legitimately change between the moment an action was recorded and the final `WindowSnapshot` state at export time (e.g., a later structural recapture per §9), and because `ElementId` (`DataModel.md` §4.7) is only guaranteed stable within a single capture rather than across recaptures, `ExportService` matches each `RecordedAction.TargetElement` against nodes in the corresponding final `WindowInformation` tree using the following precedence, stopping at the first that yields exactly one match:

1. Exact `AutomationId` match (when the action's `TargetElement.AutomationId` is non-empty), scoped to the same `WindowId`.
2. `ControlType` + `Name` + `BoundingRectangle` match within a small pixel tolerance (default ±2px, to absorb sub-pixel layout jitter), when `AutomationId` is empty or not unique.
3. If neither yields a unique match (e.g., the control was removed or restructured before export), the action's own `TargetElement` snapshot is left exactly as originally captured — this rule only affects whether the *tree copy* in `WindowInformation` gets marked, never the action's own recorded data, which is never altered.

### 17.3 Applying the marks
For every node in every `WindowInformation.RootElement` tree that matches at least one `RecordedAction.TargetElement` by the rule above: set `WasInteractedWith = true`, set `InteractionCount` to the number of matching actions, and set `InteractedActionIds` to their `ActionId`s, in `SequenceNumber` order. Every other node gets `WasInteractedWith = false`, `InteractionCount = 0`, `InteractedActionIds = []`. This computation is a pure post-processing pass over already-captured data and must not re-query the live target application (the target application may already have exited by export time, per `UC-13`'s preconditions).

### 17.4 `HierarchyExportScope` pruning algorithm
Applied by `ExportService` immediately after interaction marking (§17.3), before writing `export.json`:

| `HierarchyExportScope` value | Pruning rule |
|---|---|
| `FullTree` (default) | No pruning. Every captured element is exported, marked per §17.3. This is MVP's original behavior, retained for teams building initial Page Objects who need full sibling/context visibility. |
| `InteractedElementsWithAncestorPath` | For each `WindowInformation`, retain only nodes where `WasInteractedWith = true`, plus every ancestor of such a node up to the window's `RootElement` (needed to preserve a valid `ElementPath`/breadcrumb and a usable selector chain). All other descendant/sibling subtrees are omitted. An ancestor retained only because it leads to an interacted descendant, and which was not itself interacted with, keeps `WasInteractedWith = false` — its presence signals "structural context," not "this was touched." |
| `InteractedElementsOnly` | For each `WindowInformation`, retain only the `RootElement` itself (always required, per `DataModel.md` §4.6) and, flattened directly beneath it, only the nodes where `WasInteractedWith = true` — intermediate non-interacted ancestors are omitted entirely. Each retained node keeps its original `DepthInTree` value for reference even though it is no longer nested that many levels deep in this pruned shape. This mode is intended for a fast human sanity-check of "what did I touch," not for constructing robust selectors, since sibling/ancestor disambiguation context is discarded. |

In all three modes, `WindowInformation`'s own fields (`Title`, `ClassName`, `BoundingRectangle`, etc.) are unaffected — only the `RootElement` subtree's node count changes. `RecordingSession.Actions` is never pruned by this setting; every recorded action's own `TargetElement` snapshot is always exported in full regardless of `HierarchyExportScope`, since the Actions list (not the Windows tree) is the primary, already-minimal record of what the tester did (`UseCases.md` UC-06).

### 17.5 Interaction with `MaxHierarchyElementCount`
Pruning (§17.4) is applied after the initial capture's max-element-count truncation (§9), never before — a window that hit the safety cap during capture may already be missing some elements the tester touched near the end of a very large tree walk; pruning only ever removes *additional* non-interacted nodes from what was actually captured, it never recovers nodes the capture stage already dropped.

---

This document, together with `Architecture.md`, fully specifies runtime behavior for implementation. `DataModel.md` (next) defines the exact serializable shapes referenced throughout — `RecordingSession`, `RecordedAction`, `WindowInformation`, `ElementInformation`, `ExportPackage`, `ScreenshotInformation`, `Settings`, `ApplicationProfile`/`ApplicationLaunchChain`.
