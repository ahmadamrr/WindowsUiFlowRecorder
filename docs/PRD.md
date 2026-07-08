# Product Requirements Document (PRD)
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Master requirements reference. All other project documents (Architecture, SystemDesign, DataModel, UseCases, TestingStrategy, Roadmap, RiskAnalysis, FutureEnhancements, CodingGuidelines) must remain consistent with this document. Any conflict must be resolved by updating this PRD first.

---

## 1. Executive Summary

QA engineers who automate Windows desktop applications with FlaUI currently spend a large share of their time on manual, repetitive discovery work: opening `Inspect.exe`, hunting for `AutomationId`s and control types, walking the UI tree by hand, and then hand-authoring Page Objects and FlaUI scripts before a single automated test can run.

This project delivers a fully offline Windows desktop application — the **Windows UI Flow Recorder & Smart UI Scanner** — that watches a tester perform a manual test pass against one or more target applications and automatically captures everything an automation engineer would otherwise have collected by hand: window hierarchy, control metadata (AutomationId, ControlType, Name, ClassName, bounding rectangle, supported patterns), user actions (clicks, keystrokes, focus changes), and screenshots. All captured data is exported as structured, versioned JSON that becomes the raw material for later writing (or, in a future phase, generating) FlaUI Page Objects and test scripts.

The MVP explicitly ships **two components only**: the **Flow Recorder** and the **Smart UI Scanner**. There is no AI, no cloud dependency, and no network call anywhere in the product. The application must run entirely on an isolated, offline Windows workstation — a hard constraint driven by the target testing environments (e.g., applications that communicate with a Hardware Security Module and are frequently tested on air-gapped or restricted machines).

A distinguishing real-world requirement is that many target systems are **not single processes** — they are sequences of dependent applications. The canonical example used throughout this document is a **Proxy App** that must be launched first and must reach a "connected" state (e.g., successfully connected to a Hardware Security Module) before a second application, the **eAdmin App**, can be launched and tested. The product must treat this launch dependency as a first-class, configurable concept rather than a one-off script.

---

## 2. Product Vision

To become the standard offline capture tool that every Windows desktop QA engineer opens *before* writing a single line of FlaUI code — turning one manual test pass into a complete, structured, machine-readable record of the UI surface and the actions performed against it, for any single application or any ordered chain of dependent applications.

---

## 3. Business Problem

| Problem | Impact |
|---|---|
| Manual `Inspect.exe` usage to find `AutomationId`/`ControlType`/`Name` for every control | Hours lost per test case, repeated for every new screen |
| No structured record of what a manual tester actually did during a test pass | Automation engineers re-derive test steps from written test cases and tribal knowledge |
| Page Objects hand-written from scratch | Duplicated effort, inconsistent naming, high defect rate in early automation |
| Multi-application flows (e.g., Proxy App → HSM connection → eAdmin App) have no tooling support | Testers manually launch and sequence applications outside any capture tool, and this sequencing knowledge is not recorded anywhere |
| No offline-safe tooling for restricted/air-gapped test environments | Existing AI-assisted or cloud-based recorder tools cannot be used at all in HSM-adjacent or classified environments |

---

## 4. Goals

1. Provide a one-click way to select and launch one or more target applications, including applications with a **launch dependency chain** (App A must reach a defined ready state before App B is launched).
2. Record, with no manual tagging required, every user-initiated UI action (click, keyboard input, focus/window change) during a session.
3. For every action and every window touched during a session, automatically capture full UI Automation metadata (AutomationId, ControlType, Name, ClassName, LocalizedControlType, HelpText, bounding rectangle, supported UIA patterns) and a window/control hierarchy snapshot.
4. Automatically capture a screenshot at each significant step (configurable granularity).
5. Export the entire session as a single structured, versioned JSON package (plus a folder of referenced screenshot image files) that is self-describing and consumable by downstream tooling without needing this application.
6. Operate 100% offline: no network calls, no telemetry, no cloud storage, no AI/LLM calls in MVP.
7. Support the Proxy App → eAdmin App (or equivalent dependent-application) scenario natively, tagging every captured action/window with which target application/process it belongs to.
8. Be architected (Clean Architecture, DI, clear DTO boundaries) so that a later phase can add AI-assisted Page Object / test script generation without reworking the capture engine.

## 4.1 Non-Goals (MVP)

- No automatic generation of FlaUI Page Objects or C# test code (data export only).
- No AI/ML-based element classification, self-healing selectors, or natural-language test authoring.
- No cloud sync, licensing server, telemetry, or update-check network calls.
- No cross-platform support (Windows-only; WPF/UIA3 assume Windows Desktop).
- No support for web UI, mobile UI, or non-Win32/WinForms/WPF/UWP UI frameworks beyond what FlaUI UIA3 supports.
- No built-in test runner or CI integration (export format is designed to be consumed by such tools later, not to replace them).
- No multi-user/collaboration features; this is a single-user, single-workstation desktop tool.
- No editing of already-recorded sessions inside the app in MVP beyond basic rename/annotate/delete of a session (see Acceptance Criteria).

---

## 5. Personas

### Persona 1 — "Dara", Manual QA Tester
- Executes manual test cases daily against enterprise Windows apps.
- Not a programmer; comfortable with UI but not with `Inspect.exe` internals or C#.
- Wants: press "Record", do the test as normal, press "Stop", get a file to hand off.

### Persona 2 — "Minh", QA Automation Engineer
- Writes FlaUI Page Objects and test scripts by hand today.
- Wants: a structured JSON export with every AutomationId/ControlType/hierarchy path he needs, so he can scaffold Page Objects quickly and correctly the first time.
- Regularly tests multi-process systems (e.g., a Proxy service that must connect to a Hardware Security Module before the main admin application, eAdmin, becomes usable) and currently has no tool support for recording across that launch sequence.

### Persona 3 — "Priya", QA Lead / Automation Architect
- Owns automation strategy and coding standards for the team.
- Wants: consistent, versioned, reviewable export format across all testers; confidence that no data leaves the workstation (compliance requirement for HSM-adjacent systems); a foundation that can be extended to AI-assisted generation later without a rewrite.

---

## 6. User Stories

**Recording / Session Setup**
- As Dara, I want to select a target application by browsing to its executable or picking it from a list of running processes, so I can start recording against it without technical setup.
- As Minh, I want to configure a **launch chain** with a Primary application (e.g., Proxy App) and one or more Dependent applications (e.g., eAdmin App), each with its own launch arguments and a **readiness condition**, so the tool launches them in the correct order automatically instead of me doing it by hand outside the tool.
- As Minh, I want the readiness condition for the Primary application to be expressible as "a specific window/control appears" or "a specific window/control property equals a value" (e.g., a status label reads "HSM Connected"), so the tool knows exactly when it is safe to launch the Dependent application.
- As Dara, I want to click "Start Recording" and have the tool immediately begin tracking my actions with no further setup.

**Capture**
- As Minh, I want every mouse click on a UI element captured with the element's full UIA metadata and its position in the window hierarchy, so I don't have to re-discover it later in `Inspect.exe`.
- As Minh, I want keyboard input captured and associated with the control that had focus at the time, so I can reconstruct exact input steps.
- As Minh, I want a screenshot captured at each recorded action (or at a configurable subset of actions), so I have visual confirmation of each step.
- As Minh, I want every window that becomes active during the session (across all applications in the launch chain) captured with its full control hierarchy at least once, so I have a complete map of the UI surface, not just the controls that were clicked.
- As Priya, I want every recorded action and window snapshot tagged with which target application/process (e.g., "Proxy App" vs. "eAdmin App") it belongs to, so multi-application sessions remain unambiguous in the export.

**Session Management**
- As Dara, I want to pause and resume recording, so interruptions (phone call, unrelated window) don't pollute the capture.
- As Dara, I want to stop recording and see a summary (duration, number of actions, number of windows, number of screenshots) before exporting.
- As Priya, I want each exported session to include a schema version and tool version, so downstream tooling can detect and handle format changes safely.

**Smart UI Scanner (standalone use)**
- As Minh, I want to point the Scanner at a running application (independent of any recording session) and get a full, on-demand snapshot of its current window/control hierarchy with all UIA metadata, so I can do ad-hoc exploration without running a full recording session.
- As Minh, I want to search/filter the scanned hierarchy by AutomationId, Name, or ControlType, so I can quickly locate a specific control I need for a Page Object I'm hand-writing right now.
- As Minh, I want to export a single scan (without a recording) as its own JSON file, so I can use the Scanner as a lightweight replacement for `Inspect.exe`.

**Export**
- As Priya, I want the export to be a single JSON document (plus a screenshots folder) with a documented, versioned schema, so multiple teams can build tooling against it independently.
- As Minh, I want file paths inside the export (e.g., screenshot references) to be relative, so the exported package remains portable when copied to another machine.

---

## 7. Functional Requirements

### FR-1 Target Application Selection
- FR-1.1: The user can select a target application by (a) browsing to an executable path, (b) selecting from a list of currently running processes with visible top-level windows, or (c) selecting a previously saved **Application Profile**.
- FR-1.2: The user can define a **Dependent Application Launch Chain** consisting of one Primary application and one or more Dependent applications, each launched only after the previous step's **Readiness Condition** is satisfied.
- FR-1.3: A Readiness Condition is one of: (a) process has started and has at least one visible top-level window; (b) a specific window (matched by title pattern or AutomationId) has appeared; (c) a specific control (matched by AutomationId/Name/ControlType within a specified window) is present; (d) a specific control's Name/Value/text property matches an expected value or pattern; (e) a fixed timeout wait (documented as a fallback, not the default).
- FR-1.4: If a Readiness Condition is not satisfied within a configurable timeout, the tool must abort the launch chain, surface a clear error naming which step failed and why, and must not silently proceed.
- FR-1.5: The user can save a Target Application Selection (single app or full launch chain) as a reusable, named **Application Profile** for future sessions.

### FR-2 Recording Control
- FR-2.1: The user can Start, Pause, Resume, and Stop a recording session.
- FR-2.2: While recording, the tool attaches UI Automation event listeners to every application in the launch chain (or the single selected application) and to any additional process windows spawned by those applications during the session (e.g., a modal dialog).
- FR-2.3: The tool must visually indicate (e.g., a small always-on-top overlay/status indicator) that recording is active, paused, or stopped, so the tester never loses track of recording state.
- FR-2.4: Stopping a session finalizes it into an in-memory/on-disk **Recording Session** object and moves the UI to a summary/review screen prior to export.

### FR-3 Action Capture
- FR-3.1: The tool must capture, at minimum, the following action types: primary mouse click, secondary (right) mouse click, double-click, keyboard text entry, individual key press (non-text keys such as Enter/Tab/Escape/function keys), focus change, and window activation/switch. A mouse-down/mouse-up pair whose displacement stays within the operating system's own drag-initiation threshold must be classified as a click variant, never as a drag — the vast majority of ordinary clicks must not be misclassified as drags.
- FR-3.2: Each captured action must record: timestamp, action type, the target application/process identifier, the target window identifier, the target element's full UIA metadata snapshot at the time of the action, and the element's position within the current window hierarchy. Point-based element resolution must resolve to the most specific (deepest) element under the cursor; resolving to the window itself or to an unresolved element is acceptable only when the action genuinely targeted empty window chrome, not as a routine fallback.
- FR-3.3: Keyboard text entry must be associated with the control that held focus at the time of entry, and the captured text must match the tester's actual keystrokes exactly (one recorded character per physical keystroke, with no duplication).
- FR-3.4: The tool must debounce/coalesce rapid repeated events where appropriate (e.g., a drag producing many intermediate mouse-move events) so the export contains meaningful discrete actions rather than raw event noise. Exact coalescing rules are defined in SystemDesign.md.

### FR-4 Window & Hierarchy Capture
- FR-4.1: The first time any window becomes active during a session, the tool must capture a full control hierarchy snapshot of that window (all descendant elements and their UIA metadata) at least once.
- FR-4.2: The tool must re-capture a window's hierarchy when its structure is detected to have materially changed (e.g., a dialog is added, a list's item count changes) — subject to a configurable sensitivity/throttle setting to avoid excessive re-capture.
- FR-4.3: Each captured window must record: window title, class name, process name, process id, application/profile tag (Primary/Dependent identifier from FR-1.2), bounding rectangle, and its full descendant control tree.
- FR-4.4: The same physical window, no matter how many times it is activated or re-captured during a session, must be represented by exactly one entry in the export — re-captures update that entry in place. The tool must never produce multiple export entries for what is, on screen, a single window instance.
- FR-4.5: Every captured element must indicate whether it was the target of at least one recorded action during the session (and, if so, how many, and which ones), so a tester or automation engineer reviewing the export can distinguish the elements that were actually used from the rest of the captured hierarchy without manually cross-referencing the action list by hand. The user may additionally configure how much of a window's non-interacted hierarchy is included in the export — the full tree (default), only interacted elements plus the ancestor context needed to locate them, or only the interacted elements themselves.

### FR-5 Screenshot Capture
- FR-5.1: The tool must capture a screenshot at each recorded action by default, with a user-configurable mode to instead capture only on window change, or only at explicit user-marked checkpoints, or not at all.
- FR-5.2: Screenshots must be saved as individual image files referenced by relative path from the exported JSON, not embedded as base64 inside the JSON.
- FR-5.3: The user may optionally enable capture of a cropped screenshot of just the target element's bounding rectangle, in addition to the full window/screen screenshot.

### FR-6 Smart UI Scanner (standalone)
- FR-6.1: Independently of any recording session, the user can select a running application/window and trigger an on-demand full hierarchy scan.
- FR-6.2: The Scanner UI must present the scanned hierarchy as a navigable tree with a search/filter box supporting filtering by AutomationId, Name, ControlType, and ClassName.
- FR-6.3: Selecting a node in the Scanner tree must highlight the corresponding element on screen (visual overlay) and display its full metadata in a details panel.
- FR-6.4: The user can export a single Scanner result as its own standalone JSON document, using the same `WindowInformation`/`ElementInformation` schema used by the Flow Recorder.

### FR-7 Export
- FR-7.1: The tool must export a completed Recording Session as a single root JSON document (the `ExportPackage`) plus a folder of screenshot image files, written to a user-chosen output directory.
- FR-7.2: The export JSON must include a schema version and the producing application's version.
- FR-7.3: All file references inside the export (screenshots) must use paths relative to the export root, so the exported folder is portable.
- FR-7.4: Export must be resumable/re-triggerable: the user can re-export a previously completed session without re-recording.

### FR-8 Session & Profile Management
- FR-8.1: The user can view a list of previously recorded sessions (metadata only: name, date, target app(s), duration, action count) stored locally.
- FR-8.2: The user can rename, annotate (free-text note), or delete a previously recorded session.
- FR-8.3: The user can view, edit, duplicate, and delete saved Application Profiles (including launch chains).

### FR-9 Settings
- FR-9.1: The user can configure: screenshot mode (per FR-5.1), hierarchy re-capture sensitivity (per FR-4.2), default export directory, default readiness-condition timeout (per FR-1.4), and the hierarchy export scope — full tree, interacted-elements-with-ancestor-path, or interacted-elements-only (per FR-4.5).
- FR-9.2: All settings are stored locally and take effect without requiring an application restart, where feasible.

---

## 8. Non-Functional Requirements

| Category | Requirement |
|---|---|
| **Offline operation** | The application must make zero outbound network calls under any code path in MVP. This must be verifiable by static review and, per TestingStrategy.md, by an automated test that fails the build if a networking API is referenced outside an explicitly allow-listed module. |
| **Performance — capture overhead** | Recording must not introduce input lag perceptible to the tester (target: added latency per captured action ≤ 100ms on the reference hardware defined in SystemDesign.md). |
| **Performance — hierarchy scan** | A full hierarchy scan of a moderately complex window (defined benchmark: ≤ 2,000 descendant elements) must complete in ≤ 3 seconds. |
| **Reliability** | A crash in the target application under test must not crash the Recorder; the session up to the last successfully captured action must remain exportable. |
| **Data integrity** | Every exported session must be valid against its declared JSON schema version; the tool must validate its own export before writing it to disk. |
| **Portability of exports** | An exported package (JSON + screenshots folder) must be fully self-contained and openable/inspectable on a machine without this tool installed (i.e., plain JSON + PNG/JPEG files, no proprietary container format). |
| **Usability** | A tester with no FlaUI/automation background must be able to complete FR-1 through FR-7 (select app → record → stop → export) without training, using only in-app guidance. |
| **Resource usage** | The tool must not exceed a documented memory ceiling (see SystemDesign.md) during a typical 30-minute recording session with screenshot capture enabled. |
| **Security/compliance** | No data is transmitted or persisted outside the local filesystem location chosen by the user. No credentials, HSM material, or target-application secrets are ever read, logged, or included in captures beyond what is visibly rendered as UI text. |
| **Extensibility** | Capture engine, storage, and export layers must be decoupled (Clean Architecture boundaries) so that a future AI-assisted generation module can consume `ExportPackage` data without modifying the capture engine. |

---

## 9. Constraints

- Must run on Windows 10/11 desktop only.
- Must be built with C#, .NET 8, WPF (MVVM), FlaUI (UIA3 provider), Clean Architecture, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, System.Text.Json, and tested with xUnit/FluentAssertions/Moq, per the technology mandate.
- No third-party cloud SDKs, telemetry SDKs, or auto-update mechanisms may be included in MVP.
- Target applications under test may themselves be built on any Windows UI technology exposing standard UI Automation (Win32, WinForms, WPF, UWP) and any .NET runtime (including .NET Framework, as with the Proxy App / eAdmin App reference scenario); the Recorder's own runtime is independent of the target applications' runtime.
- The tool must function correctly on a fully air-gapped machine (no internet access available at all, not merely blocked).

---

## 10. Assumptions

- Target applications expose meaningful UI Automation metadata (AutomationId/Name/ControlType) for at least their primary interactive controls. Applications with extremely poor UIA support are a known limitation, documented in RiskAnalysis.md, not a blocking requirement for MVP.
- The reference multi-application scenario (Proxy App must reach an HSM-connected state before eAdmin App is launched) is representative of, but not limited to, the kinds of dependent-application chains the tool must support; the launch-chain feature is designed generically (N steps, not hardcoded to 2 apps or to HSM semantics).
- Testers performing recordings have local administrative rights sufficient to launch the target application(s) and to allow the Recorder to attach UI Automation listeners to them (no elevation-bypass features are in scope).
- A single physical workstation is used for both the target application(s) and the Recorder (no remote desktop / cross-machine automation in MVP).

---

## 11. Risks

See RiskAnalysis.md for the full register. Top risks affecting this PRD's scope:

1. **UIA coverage gaps**: some legacy Win32 controls expose minimal automation metadata, degrading capture quality for those specific controls.
2. **Readiness-condition fragility**: a Readiness Condition defined against a specific control/text (e.g., "HSM Connected" label) breaks if the target application's UI text or structure changes.
3. **Performance under heavy screenshot capture**: full-screen screenshots on every action across a long session may cause export packages to grow very large.
4. **Multi-window/multi-process event volume**: attaching listeners across an entire launch chain (Proxy + eAdmin + any child dialogs) increases the risk of missed or duplicated events during rapid window switching.

---

## 12. Success Metrics

- Time to produce a "ready-to-scaffold" data set for a new test case's Page Object drops by at least 70% compared to the current manual `Inspect.exe` workflow (measured via a before/after time study with the QA team).
- 100% of exported sessions validate against the published JSON schema (zero invalid exports in QA sign-off testing).
- Zero outbound network calls detected during a full audit/instrumented test run (see NFR "Offline operation").
- The Proxy App → eAdmin App reference launch chain completes successfully (both apps launched, readiness condition detected correctly) in at least 95% of attempts under normal conditions during acceptance testing.

---

## 13. Acceptance Criteria (MVP)

The MVP is considered acceptance-ready when all of the following are demonstrably true:

1. A tester can configure a single-application target, start recording, perform at least 10 distinct UI actions across at least 2 different windows, stop recording, and export a valid `ExportPackage` JSON plus screenshots folder.
2. A tester can configure a two-step Dependent Application Launch Chain (Primary + one Dependent), define a control-based Readiness Condition on the Primary, and have the tool launch the Dependent application automatically only once that condition is met — including a demonstrable "aborts cleanly with a clear error" behavior when the condition is never met.
3. Every recorded action in an exported session includes complete UIA metadata for its target element and correctly tags which application in the launch chain it belongs to.
4. Every window activated during the session appears **exactly once** in the export with its (final, in-place-updated) full hierarchy snapshot — a window re-activated or re-captured multiple times during a session must never produce more than one export entry for it.
5. The Smart UI Scanner can, independently of any recording session, scan a running application on demand, present a searchable hierarchy tree, highlight elements on screen, and export a standalone scan as valid JSON using the shared schema.
6. No network call occurs at any point during any of the above flows (verified by instrumented/automated test, per TestingStrategy.md).
7. All exported JSON validates against the schema version declared inside the export itself.
8. A previously recorded session can be renamed, annotated, deleted, and re-exported without being re-recorded.
9. In an exported session, every element that was the target of a recorded action is marked as such (with an accurate interaction count and links back to the specific action(s)), and switching `HierarchyExportScope` between its three modes visibly changes the exported hierarchy size accordingly, without altering the `Actions` list itself.
10. Given a scripted test pass with a known, fixed sequence of clicks and a known typed string, the exported session contains no drag-misclassified clicks, no unresolved (`Unknown`/`None`) target elements, and an `EnteredText` value that is character-for-character identical to what was actually typed.

---

## 14. Release Plan

MVP is released as a single internal build once all Acceptance Criteria in Section 13 pass. See Roadmap.md for the phase-by-phase implementation plan (not included in this document); Architecture.md defines the technical structure that all phases build against. No phased *external* release is planned in MVP — this is an internal QA tooling release, distributed to the QA team as a single installable/portable build once acceptance passes.
