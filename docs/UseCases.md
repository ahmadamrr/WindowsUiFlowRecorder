# Use Cases
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Actor-level use cases derived from `PRD.md` (Functional Requirements, Personas) and grounded in the behavior defined in `Architecture.md` and `SystemDesign.md`. Each use case maps to one or more FR-x items and must not describe behavior inconsistent with those documents. `DataModel.md` entities referenced here use their canonical names.

---

## 1. Actors

| Actor | Description |
|---|---|
| **Dara** (Manual QA Tester) | Primary actor for recording flows; no automation/programming background. |
| **Minh** (QA Automation Engineer) | Primary actor for Smart UI Scanner flows, profile/launch-chain configuration, and export consumption. |
| **Priya** (QA Lead / Automation Architect) | Secondary actor for profile governance and export-schema consistency across the team. |
| **Target Application** | Secondary/system actor — the Proxy App, eAdmin App, or any other application under test; initiates events the system must react to (e.g., crashing, reaching a readiness state). |
| **System** | The Windows UI Flow Recorder & Smart UI Scanner application itself, acting autonomously in response to Target Application or OS events (e.g., detecting a crash, re-capturing a changed hierarchy). |

---

## 2. Use Case Index

| ID | Title | Primary Actor | Related FRs |
|---|---|---|---|
| UC-01 | Select a Single Target Application | Dara, Minh | FR-1.1 |
| UC-02 | Configure a Dependent Application Launch Chain | Minh | FR-1.2–FR-1.4 |
| UC-03 | Save an Application Profile | Minh | FR-1.5 |
| UC-04 | Start a Recording Session | Dara | FR-2.1, FR-1.2–FR-1.4 |
| UC-05 | Pause and Resume Recording | Dara | FR-2.1, FR-2.3 |
| UC-06 | Capture a User Action | System | FR-3.1–FR-3.4 |
| UC-07 | Capture Window Hierarchy on Window Activation | System | FR-4.1–FR-4.3 |
| UC-08 | Handle Target Application Crash During Recording | System | Reliability NFR |
| UC-09 | Continue Recording Across Multiple Application Windows/Processes | System, Dara | FR-2.2, FR-3.2, Multi-app design |
| UC-10 | Capture a Screenshot During an Action | System | FR-5.1–FR-5.3 |
| UC-11 | Handle Launch Chain Readiness Timeout | System | FR-1.4 |
| UC-12 | Stop Recording and Review Session Summary | Dara | FR-2.4 |
| UC-13 | Export a Recording Session | Minh, Dara | FR-7.1–FR-7.3 |
| UC-14 | Re-export a Previously Recorded Session | Minh | FR-7.4 |
| UC-15 | Perform a Standalone UI Scan | Minh | FR-6.1 |
| UC-16 | Search and Filter a Scanned Hierarchy | Minh | FR-6.2 |
| UC-17 | Highlight an Element On Screen from the Scanner | Minh | FR-6.3 |
| UC-18 | Export a Standalone Scan | Minh | FR-6.4 |
| UC-19 | Manage Recorded Sessions | Dara, Minh | FR-8.1–FR-8.2 |
| UC-20 | Manage Application Profiles | Minh, Priya | FR-8.3 |
| UC-21 | Configure Application Settings | Minh, Priya | FR-9.1–FR-9.2 |

---

## UC-01 — Select a Single Target Application

**Primary Actor:** Dara or Minh
**Related FRs:** FR-1.1

**Preconditions:**
- The application is running and in the `Idle` or `Configuring` state (`SystemDesign.md` §3).
- The target application executable exists on the local machine, or is already running.

**Main Flow:**
1. Tester opens Target Application Selection.
2. Tester chooses one of: browse to an executable path, pick from a list of running processes with visible top-level windows, or select a previously saved single-application `ApplicationProfile`.
3. System validates the selection (executable exists / process is currently running).
4. System transitions to `Configuring` with the selection held, ready for `UC-04 Start a Recording Session`.

**Alternate/Exception Flows:**
- 3a. Selected executable path does not exist → System shows a clear inline error; tester remains on the selection screen.
- 3b. Selected running process has no visible top-level window → System excludes it from the running-process list in step 2 (it is never offered as a candidate).

**Postconditions:**
- A single-step `ApplicationLaunchChain` (one `LaunchStep`) is held in memory, ready to be launched.

---

## UC-02 — Configure a Dependent Application Launch Chain

**Primary Actor:** Minh
**Related FRs:** FR-1.2, FR-1.3, FR-1.4

**Preconditions:**
- The application is in `Idle` or `Configuring` state.
- Minh knows the executable paths and desired readiness behavior for each application in the chain (e.g., Proxy App and eAdmin App).

**Main Flow (worked using the Proxy App → eAdmin App / HSM reference scenario):**
1. Minh opens the Launch Chain Builder.
2. Minh adds Step 1 (Primary): sets `ApplicationTag = "ProxyApp"`, executable path, optional arguments.
3. Minh configures Step 1's `ReadinessCondition`: selects `ControlPropertyEquals`, scopes it to the Proxy App's main window, selects the status label control (by `AutomationId`), and sets the expected value (e.g., contains `"Connected"`).
4. Minh adds Step 2 (Dependent): sets `ApplicationTag = "eAdminApp"`, executable path, optional arguments.
5. Minh configures Step 2's `ReadinessCondition`: selects `WindowAppeared`, scoped to a title pattern matching eAdmin App's main window.
6. Minh optionally overrides the default readiness timeout per step and the "clean up on failure" flag.
7. System validates the chain (step ordering contiguous, at least one match criterion set per condition, per `DataModel.md` §6.4).
8. System holds the validated `ApplicationLaunchChain` in memory, ready for `UC-04` or `UC-03`.

**Alternate/Exception Flows:**
- 7a. A `ControlPropertyEquals`/`ControlPresent` condition has no `ElementAutomationId`, `ElementName`, or `ElementControlType` set → System blocks saving/starting with an inline validation error (`DataModel.md` §6.4 requires at least one).
- 3a. Minh is unsure of the exact `AutomationId` for the status label → Minh switches to the Smart UI Scanner (`UC-15`) against an already-running Proxy App to look it up, then returns to this flow.

**Postconditions:**
- A fully validated N-step `ApplicationLaunchChain` is ready to launch or be saved as a profile.

---

## UC-03 — Save an Application Profile

**Primary Actor:** Minh
**Related FRs:** FR-1.5

**Preconditions:**
- A valid single-application selection (UC-01) or launch chain (UC-02) is currently configured.

**Main Flow:**
1. Minh clicks "Save as Profile."
2. Minh enters a profile name (e.g., `"Proxy + eAdmin (HSM)"`) and optional description.
3. System persists the `ApplicationProfile` via the profile repository, generating a new `ProfileId`.
4. System confirms the save and the profile becomes available in future `UC-01`/`UC-20` flows.

**Alternate/Exception Flows:**
- 3a. A profile with the same name already exists → System prompts Minh to rename or overwrite; overwrite requires explicit confirmation.

**Postconditions:**
- The profile is persisted locally and reusable without reconfiguring the chain from scratch.

---

## UC-04 — Start a Recording Session

**Primary Actor:** Dara
**Related FRs:** FR-2.1, FR-1.2–FR-1.4 (launch chain execution)

**Preconditions:**
- A target selection or launch chain is configured (UC-01/UC-02) or loaded from a saved profile.
- System is in `Configuring` state.

**Main Flow:**
1. Dara clicks "Start Recording."
2. System transitions to `LaunchingChain` and begins executing the configured chain step by step (`SystemDesign.md` §4.1): launch Step 1 (Proxy App), poll its `ReadinessCondition`, then launch Step 2 (eAdmin App) once satisfied.
3. Once all steps succeed, System builds the `TargetApplicationContext` list, attaches UIA and input listeners to all of them, and transitions to `Recording`.
4. System displays the always-on-top Recording Status Overlay indicating "Recording" (FR-2.3).
5. Dara performs the manual test pass as normal against Proxy App and/or eAdmin App windows.

**Alternate/Exception Flows:**
- 2a. A step's readiness condition times out → see `UC-11 Handle Launch Chain Readiness Timeout`; System never enters `Recording`.
- 1a. Dara starts recording with only a single application configured (no chain) → step 2 is skipped; System launches the one application and proceeds directly once it has at least one visible window.

**Postconditions:**
- A new `RecordingSession` exists in `Recording` state with one or more active `TargetApplicationContext`s.

---

## UC-05 — Pause and Resume Recording

**Primary Actor:** Dara
**Related FRs:** FR-2.1, FR-2.3

**Preconditions:**
- Session is in `Recording` state.

**Main Flow:**
1. Dara is interrupted (e.g., an unrelated phone call) and clicks "Pause" on the overlay.
2. System transitions to `Paused`, detaches active input capture (no actions are recorded while paused), and updates the overlay to show "Paused."
3. Dara resolves the interruption and clicks "Resume."
4. System transitions back to `Recording`, re-attaches capture, and updates the overlay to show "Recording."

**Alternate/Exception Flows:**
- 1a. Dara clicks "Stop" instead of "Resume" while paused → proceeds directly to `UC-12`.

**Postconditions:**
- No actions are recorded for the duration between `Pause` and `Resume`; the session's action sequence has no gap markers (a paused interval is simply absent from `Actions`, not represented as an entry).

---

## UC-06 — Capture a User Action

**Primary Actor:** System (triggered by Dara's physical input)
**Related FRs:** FR-3.1–FR-3.4

**Preconditions:**
- Session is in `Recording` state.
- Dara performs a mouse click, keyboard entry, or focus/window change against a window belonging to an active `TargetApplicationContext`.

**Main Flow:**
1. The OS-level input hook observes the raw input event (`SystemDesign.md` §6).
2. System resolves the `AutomationElement` at the event's point (for clicks) or the focused element (for keyboard/focus events) via the UIA adapter (`SystemDesign.md` §7).
3. System applies the coalescing policy (`SystemDesign.md` §8) — e.g., grouping keystrokes into a single `TextEntry` action, or a drag gesture into a single `Drag` action.
4. System builds a `RecordedAction` with full `ElementInformation`, `ElementPath`, `ApplicationTag`, and `WindowId`.
5. System appends the action to the session's `Actions` list in sequence order.

**Alternate/Exception Flows:**
- 2a. The resolved element belongs to a process outside the current launch chain (e.g., Dara briefly alt-tabs away) → System records this only as a window-deactivation boundary, not as a `RecordedAction` (`SystemDesign.md` §7).
- 2b. The UIA element reference is stale by the time properties are read → System treats this as "not found" and retries on the next input event rather than raising an error to Dara.

**Postconditions:**
- Exactly one new `RecordedAction` (or none, if step 2a applies) is appended to the session.

---

## UC-07 — Capture Window Hierarchy on Window Activation

**Primary Actor:** System
**Related FRs:** FR-4.1–FR-4.3

**Preconditions:**
- Session is in `Recording` state.
- A window belonging to an active `TargetApplicationContext` becomes active (first time, or after a structural change).

**Main Flow:**
1. System detects the window activation via a subscribed UIA event and looks up the window's native OS handle against the session's already-tracked window handles (`SystemDesign.md` §9).
2. If this handle has never been seen before in this session, System mints a new `WindowId` for it and performs a full depth-first hierarchy walk (`SystemDesign.md` §9) up to the configured `MaxHierarchyElementCount`.
3. If this handle already has a `WindowSnapshot` (i.e., this is a re-activation of the same physical window — including the tester switching back to a window they already visited, per `UC-09`), System reuses the existing `WindowId` and computes the window's structural fingerprint to compare against the stored one, rather than creating a new entry.
4. If the fingerprint differs and the minimum re-capture interval has elapsed, System re-walks the hierarchy and replaces the stored `WindowSnapshot` in place, under the same `WindowId`.
5. System stores/updates the `WindowSnapshot` keyed by `WindowId` in the session's `Windows` collection — one entry per distinct physical window for the entire session, regardless of how many times it was activated.

**Alternate/Exception Flows:**
- 2a. The hierarchy exceeds `MaxHierarchyElementCount` → System truncates the walk at the safety limit and logs a warning; the resulting `WindowInformation` is still exported, but is understood to be a partial tree.
- 3a. The tester re-activates a window they already visited earlier in the session and nothing about it has structurally changed → System recognizes the existing handle, confirms the fingerprint is unchanged, and makes no update at all — no new entry, no unnecessary re-walk.
- 4a. Fingerprint differs but the minimum re-capture interval has not elapsed → System defers the re-capture until the next qualifying trigger (`SystemDesign.md` §9).

**Postconditions:**
- The session holds exactly one current `WindowSnapshot` per distinct window touched so far.

---

## UC-08 — Handle Target Application Crash During Recording

**Primary Actor:** System
**Related:** PRD NFR "Reliability", `SystemDesign.md` §14

**Preconditions:**
- Session is in `Recording` state with one or more active `TargetApplicationContext`s.
- One of the target processes (e.g., eAdmin App) crashes or exits unexpectedly.

**Main Flow:**
1. `IProcessLaunchMonitor` detects the process exit.
2. System marks the corresponding `TargetApplicationContext.IsActive = false` and sets `TerminationReason = ProcessCrashed`.
3. System detaches UIA/input subscriptions scoped to that context only.
4. If at least one other `TargetApplicationContext` remains active (e.g., Proxy App is still running), recording continues uninterrupted against the surviving application(s) — see `UC-09`.
5. If no context remains active, System automatically transitions the session to `Stopped` and proceeds to `UC-12`, preserving every action/window captured so far.

**Alternate/Exception Flows:**
- 4a. The crashed application later restarts on its own (outside the tool's control) → System does **not** automatically re-attach; the tester must stop and start a new session if they want to resume recording against it, since the process id has changed.

**Postconditions:**
- No data captured prior to the crash is lost; the session remains fully exportable per NFR "Reliability."

---

## UC-09 — Continue Recording Across Multiple Application Windows/Processes

**Primary Actor:** System, observed by Dara
**Related FRs:** FR-2.2, FR-3.2 (multi-application session design, `Architecture.md` §1 principle 4)

**Preconditions:**
- Session is in `Recording` state with two or more active `TargetApplicationContext`s (e.g., Proxy App and eAdmin App both running, per `UC-04`).

**Main Flow:**
1. Dara performs actions in the eAdmin App window (e.g., filling a form).
2. Dara switches focus to the Proxy App window (e.g., to check connection status) and performs an action there.
3. System resolves each action's owning `TargetApplicationContext` by process id (`SystemDesign.md` §7) and tags the resulting `RecordedAction.ApplicationTag` accordingly (`"eAdminApp"` then `"ProxyApp"`), with no manual tagging required from Dara.
4. Dara switches back to eAdmin App and continues the test pass; this switching can repeat any number of times for the remainder of the session.
5. Any new window belonging to either application (e.g., a modal dialog spawned by eAdmin App) is automatically discovered and captured per `UC-07`, without Dara needing to reconfigure anything.

**Alternate/Exception Flows:**
- 1a. Dara switches to a window belonging to neither tracked application → handled per `UC-06` alternate flow 2a (recorded only as a boundary, not as an action).
- 2a. One of the two applications crashes mid-switch → handled per `UC-08`; the surviving application's actions continue to be captured without interruption.

**Postconditions:**
- The exported session (`UC-13`) unambiguously attributes every action and window to the correct application via `ApplicationTag`, regardless of how many times Dara switched between them during the session.

---

## UC-10 — Capture a Screenshot During an Action

**Primary Actor:** System
**Related FRs:** FR-5.1–FR-5.3

**Preconditions:**
- Session is in `Recording` state.
- `Settings.ScreenshotMode` is not `Off`.
- A `RecordedAction` is being finalized per `UC-06`.

**Main Flow:**
1. System determines whether this action qualifies for a screenshot based on `Settings.ScreenshotMode` (every action / window-change-only / manual-checkpoint-only).
2. System captures the configured scope: full screen, the owning window's bounds, and — if `Settings.CaptureElementCroppedScreenshot` is enabled — an additional element-bounded crop.
3. System writes the image asynchronously to the session's working screenshot folder using the sequential naming convention (`SystemDesign.md` §10).
4. System creates a `ScreenshotReference`/`ScreenshotInformation` entry and links it via `RecordedAction.ScreenshotId`.

**Alternate/Exception Flows:**
- 3a. The screenshot write queue exceeds the backpressure threshold → System temporarily degrades capture scope (e.g., window-only instead of full-screen) and logs a warning, rather than blocking Dara's input (`SystemDesign.md` §10).

**Postconditions:**
- Zero or more new `ScreenshotInformation` entries are added, each referencing a real file in the working screenshot folder.

---

## UC-11 — Handle Launch Chain Readiness Timeout

**Primary Actor:** System
**Related FRs:** FR-1.4

**Preconditions:**
- Session is in `LaunchingChain` state (`UC-04` in progress).
- A step's `ReadinessCondition` (e.g., Proxy App's HSM-connected status) is not satisfied within its configured timeout.

**Main Flow:**
1. System's poll loop for the current step exceeds the effective timeout (profile override or `Settings.DefaultReadinessConditionTimeoutSeconds`).
2. System aborts the launch chain — no further steps are launched.
3. If the failed step's `CleanUpOnFailure` flag is set, System terminates any processes already started during this launch attempt.
4. System transitions to `LaunchFailed` and surfaces an error naming the specific step and condition that was not met (e.g., `"ProxyApp: control [lblHsmStatus] Value never contained \"Connected\" within 30s"`).
5. Dara/Minh returns to `Configuring` to adjust the profile (e.g., correct the `AutomationId`, increase the timeout) and retries via `UC-04`.

**Alternate/Exception Flows:**
- 1a. The target process exits/crashes before its condition is ever met → System aborts immediately without waiting out the remaining timeout (`SystemDesign.md` §5).
- 1b. More than the configured number of unexpected (non-"not found") errors occur during polling → System fails the step early with the underlying error surfaced, rather than waiting out the full timeout (`SystemDesign.md` §5).

**Postconditions:**
- No `RecordingSession` ever enters `Recording` with a partially-launched chain; recording either starts fully configured or not at all.

---

## UC-12 — Stop Recording and Review Session Summary

**Primary Actor:** Dara
**Related FRs:** FR-2.4

**Preconditions:**
- Session is in `Recording` or `Paused` state.

**Main Flow:**
1. Dara clicks "Stop."
2. System transitions to `Stopped`, detaches all remaining UIA/input subscriptions.
3. System computes summary statistics: duration, action count, window count, screenshot count, and per-application breakdown.
4. System transitions to `Reviewing` and displays the summary alongside an option to rename/annotate the session (FR-8.2) before export.

**Alternate/Exception Flows:**
- 2a. Recording was already auto-stopped due to a full crash (`UC-08` step 5) → System still displays the summary, additionally noting the termination reason (`AllTargetsCrashedOrExited`).

**Postconditions:**
- The session is fully finalized and ready for `UC-13`; no further actions can be appended to it.

---

## UC-13 — Export a Recording Session

**Primary Actor:** Minh or Dara
**Related FRs:** FR-7.1–FR-7.3, FR-4.5

**Preconditions:**
- Session is in `Reviewing` state (`UC-12` complete).

**Main Flow:**
1. Tester clicks "Export" and chooses an output directory (pre-filled from `Settings.DefaultExportDirectory`, if set), optionally overriding `Settings.HierarchyExportScope` for this export only.
2. System transitions to `Exporting`, maps the internal `RecordingSession` aggregate into an `ExportPackage`/`RecordingSessionExport` (`DataModel.md` §4).
3. System marks every captured element that was the target of a recorded action (`WasInteractedWith`, `InteractionCount`, `InteractedActionIds`) and applies the chosen `HierarchyExportScope` pruning rule to each window's tree (`SystemDesign.md` §17).
4. System validates the resulting document against the declared `SchemaVersion` before writing anything to disk.
5. System writes `export.json` and copies/relocates all referenced screenshots into the chosen folder, rewriting paths as relative (FR-7.3).
6. System transitions to `Exported` and confirms success with the output path.

**Alternate/Exception Flows:**
- 4a. Validation fails (should not occur in a correctly implemented system, but is checked defensively) → System blocks the write and surfaces a clear internal error rather than producing a partially-invalid export.
- 5a. Disk write fails (e.g., destination full or inaccessible) → System reports a structured failure; the session remains in `Reviewing`/working storage, unmodified, so export can be retried (`UC-14`) without re-recording.

**Postconditions:**
- A self-contained, portable export folder (`export.json` + `screenshots/`) exists at the chosen location.

---

## UC-14 — Re-export a Previously Recorded Session

**Primary Actor:** Minh
**Related FRs:** FR-7.4

**Preconditions:**
- A session in `Exported` (or `Reviewing`, if never exported) state exists in the local Session List (`UC-19`).

**Main Flow:**
1. Minh selects a prior session from the Session List and clicks "Export" (or "Re-export").
2. System repeats `UC-13` steps 2–5 against the stored working session data, without requiring any re-recording.

**Alternate/Exception Flows:**
- 1a. Minh chooses a different output directory than the original export → both exports coexist independently; the original is untouched.

**Postconditions:**
- A new, independent export folder is produced from the same underlying session data.

---

## UC-15 — Perform a Standalone UI Scan

**Primary Actor:** Minh
**Related FRs:** FR-6.1

**Preconditions:**
- The target application (e.g., Proxy App, running independently of any recording session) is already running.

**Main Flow:**
1. Minh opens the Smart UI Scanner and selects the running application/window.
2. Minh clicks "Scan."
3. System performs an on-demand full hierarchy walk of the selected window (same walker as `UC-07`, but not tied to a `RecordingSession`).
4. System displays the resulting hierarchy as a navigable tree.

**Alternate/Exception Flows:**
- 3a. The window's hierarchy exceeds `MaxHierarchyElementCount` → same truncation-with-warning behavior as `UC-07` alternate flow 2a.

**Postconditions:**
- A `WindowInformation` tree is held in memory, ready for `UC-16`–`UC-18`.

---

## UC-16 — Search and Filter a Scanned Hierarchy

**Primary Actor:** Minh
**Related FRs:** FR-6.2

**Preconditions:**
- A scan result exists in memory (`UC-15` complete).

**Main Flow:**
1. Minh types a search term into the filter box.
2. Minh selects which field(s) to match against: `AutomationId`, `Name`, `ControlType`, and/or `ClassName`.
3. System filters the displayed tree to matching elements (and their ancestor chain, so matches remain visible in context).

**Alternate/Exception Flows:**
- 3a. No elements match → System shows an empty-state message rather than an empty, ambiguous tree.

**Postconditions:**
- The tree view reflects only matching elements until the filter is cleared.

---

## UC-17 — Highlight an Element On Screen from the Scanner

**Primary Actor:** Minh
**Related FRs:** FR-6.3

**Preconditions:**
- A scan result is displayed (`UC-15`/`UC-16`).

**Main Flow:**
1. Minh selects a node in the hierarchy tree.
2. System draws an on-screen highlight overlay around that element's live `BoundingRectangle` on the actual target application window.
3. System displays the element's full `ElementInformation` in the details panel.

**Alternate/Exception Flows:**
- 2a. The element is no longer present/has gone stale since the scan (e.g., the target app's UI changed) → System indicates the element could not be located live, while still showing its last-known captured metadata in the details panel.

**Postconditions:**
- Minh can visually confirm which control on screen corresponds to which tree node without touching `Inspect.exe`.

---

## UC-18 — Export a Standalone Scan

**Primary Actor:** Minh
**Related FRs:** FR-6.4

**Preconditions:**
- A scan result exists (`UC-15`).

**Main Flow:**
1. Minh clicks "Export Scan" and chooses an output directory.
2. System maps the scan into a `StandaloneScanExport` inside an `ExportPackage` with `ExportKind = StandaloneScan`, using the same `WindowInformation`/`ElementInformation` shapes as a recording export (`DataModel.md` §4.3).
3. System validates and writes `export.json`, following the same write path as `UC-13`.

**Alternate/Exception Flows:**
- Same as `UC-13` alternate flows 3a/4a.

**Postconditions:**
- A standalone, schema-valid export exists, usable identically to a recording-session export by downstream tooling.

---

## UC-19 — Manage Recorded Sessions

**Primary Actor:** Dara or Minh
**Related FRs:** FR-8.1, FR-8.2

**Preconditions:**
- One or more sessions exist locally.

**Main Flow:**
1. Tester opens the Session List, showing `SessionListItem` projections (name, date, target apps, duration, action count).
2. Tester renames a session, adds/edits a free-text `Note`, or deletes a session.
3. System persists the change (or removal) via the session repository.

**Alternate/Exception Flows:**
- 2a. Tester deletes a session → System asks for confirmation before permanently removing the working session data and any un-exported screenshots.

**Postconditions:**
- The Session List and underlying storage reflect the tester's changes.

---

## UC-20 — Manage Application Profiles

**Primary Actor:** Minh or Priya
**Related FRs:** FR-8.3

**Preconditions:**
- One or more `ApplicationProfile`s exist locally (e.g., the saved Proxy + eAdmin chain from `UC-03`).

**Main Flow:**
1. Tester opens the Application Profile manager.
2. Tester views, edits (re-opens the Launch Chain Builder from `UC-02` pre-filled), duplicates, or deletes a profile.
3. System persists the change via the profile repository.

**Alternate/Exception Flows:**
- 2a. Tester deletes a profile referenced by an existing exported session → the export is unaffected (it already embedded a `LaunchChainInformation` snapshot per `DataModel.md` §4.9); only future use of that profile is no longer possible.

**Postconditions:**
- The profile list and underlying storage reflect the tester's changes.

---

## UC-21 — Configure Application Settings

**Primary Actor:** Minh or Priya
**Related FRs:** FR-9.1, FR-9.2

**Preconditions:**
- None (Settings always exist with defaults from first run).

**Main Flow:**
1. Tester opens Settings.
2. Tester adjusts one or more values: screenshot mode, element-cropped screenshot toggle, hierarchy re-capture sensitivity, hierarchy export scope (full tree / interacted-with-ancestors / interacted-only), default export directory, default readiness-condition timeout, verbose diagnostic logging toggle.
3. System persists the change immediately via the settings repository and applies it going forward without requiring a restart, where feasible (FR-9.2).

**Alternate/Exception Flows:**
- 2a. Tester enables `VerboseDiagnosticLoggingEnabled` → System displays an explicit warning that this may log captured UI text content locally, per `SystemDesign.md` §16, requiring acknowledgment before it takes effect.

**Postconditions:**
- `Settings` reflects the tester's chosen configuration and is used by all subsequent sessions/scans until changed again.
