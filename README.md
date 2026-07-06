# Windows UI Flow Recorder & Smart UI Scanner

> Offline Windows desktop tool that records manual QA testing sessions against Windows desktop applications (via FlaUI/UIA3) and exports structured JSON containing UI Automation metadata, screenshots, and window hierarchy — eliminating the manual Inspect.exe / AutomationId-hunting workflow for FlaUI test authors.

---

## 1. What This Project Is

QA engineers who automate Windows desktop applications with **FlaUI** currently spend a large portion of their time doing manual, repetitive discovery work: opening `Inspect.exe`, clicking through a UI hierarchy, copying `AutomationId`s and `ControlType`s by hand, and then hand-writing Page Objects and FlaUI scripts.

This project replaces that manual discovery loop with two integrated capabilities:

| Component | Purpose |
|---|---|
| **Flow Recorder** | Launches target applications (including multi-step launch chains), watches the tester perform a manual test case, and records every user action (clicks, keystrokes, focus changes) together with full UI Automation metadata, screenshots, and window hierarchy snapshots. |
| **Smart UI Scanner** | On demand, walks the full UI Automation tree of a selected window/application, displays a searchable hierarchy tree, and exports a structured snapshot — independent of any recording session. |

The output of both components is **structured JSON**, designed to be the deterministic, machine-readable input for a *future* AI-assisted Page Object / FlaUI code generator. **No AI, cloud, or network dependency is part of this project.** Everything runs fully offline on the tester's Windows machine.

---

## 2. MVP Features

### Flow Recorder (Phases 0-7)

| Feature | Status |
|---|---|
| **Target Application Profiles** — save and reuse application launch configurations | ✅ |
| **Launch Chain** — configure Primary + Dependent applications with Readiness Conditions (process-started, window-appeared, control-present, control-property-equals, fixed-timeout) | ✅ |
| **Start/Pause/Resume/Stop** recording with full state machine (Idle → Configuring → LaunchingChain → Recording → Paused → Stopped → Reviewing → Exporting → Exported) | ✅ |
| **Global Input Capture** — low-level Win32 mouse/keyboard hooks, thread-safe producer/consumer queue | ✅ |
| **Action Coalescing** — drag gestures, text entry, duplicate window activations collapsed into meaningful discrete actions | ✅ |
| **UIA Element Correlation** — element lookup by point (clicks) and by focus (keyboard), process-id scoped to active target contexts | ✅ |
| **Window Hierarchy Capture** — depth-first UIA tree walk with configurable max-element safety limit (default 5,000) | ✅ |
| **Structural Fingerprinting** — SHA-256 hashing of ControlType/AutomationId/depth to detect structural changes | ✅ |
| **Re-capture on Change** — windows are re-captured when the fingerprint changes (subject to configurable sensitivity: Low=2s, Medium=500ms, High=100ms intervals) | ✅ |
| **Screenshot Capture** — full-screen, window-bounded, or element-bounded PNG screenshots at configurable granularity | ✅ |
| **Always-on-top Recording Overlay** — blinking red indicator with paused state (orange) | ✅ |
| **Input Hook Heartbeat** — detects if the OS silently disables the hook and warns the tester | ✅ |
| **Target Process Crash Detection** — auto-stops the session if all target processes exit | ✅ |
| **Export Pipeline** — versioned ExportPackage JSON + screenshots folder with schema self-validation | ✅ |
| **Folder Picker** — choose export destination via OS folder dialog | ✅ |
| **Re-export** — export the same session multiple times to different locations | ✅ |

### Smart UI Scanner (Phase 8)

| Feature | Status |
|---|---|
| **Process Picker** — enumerate running processes with visible windows | ✅ |
| **On-demand Hierarchy Scan** — full UIA tree walk of any window | ✅ |
| **Search/Filter Tree** — filter by AutomationId, Name, ControlType, ClassName with live parent visibility propagation | ✅ |
| **Element Details Panel** — AutomationId, Name, ControlType, ClassName, bounding rectangle, patterns, children count, enabled/offscreen/focusable state | ✅ |
| **On-screen Highlight** — transparent red-border overlay positioned over the selected element | ✅ |
| **Standalone Export** — export scan results as standalone ExportPackage JSON using the shared schema | ✅ |

### Session & Profile Management (Phase 9)

| Feature | Status |
|---|---|
| **Session History** — list all recorded sessions with metadata (date, duration, action/window/screenshot counts) | ✅ |
| **Rename & Annotate** — edit session name and notes | ✅ |
| **Delete Sessions** — remove old session data | ✅ |
| **Profile Manager** — view, edit name/description, save | ✅ |
| **Duplicate Profiles** — clone existing profiles with a new name | ✅ |
| **Delete Profiles** — remove unused profiles | ✅ |
| **Settings** — screenshot mode, hierarchy sensitivity, default export directory, readiness timeout/poll interval, max hierarchy elements, verbose logging | ✅ |

### Architecture Compliance (Phase 10)

| Feature | Status |
|---|---|
| **No Network Access** — automated tests scan all 4 layers for System.Net/Http/Sockets namespaces | ✅ |
| **Clean Architecture Direction** — Domain→Application→Infrastructure, Presentation only references Infrastructure in composition root | ✅ |
| **No FlaUI Leakage** — FlaUI types never appear in Domain, Application, or Presentation | ✅ |
| **One-class-per-file** — enforced by convention | ✅ |
| **Nullable Reference Types** — enabled solution-wide with build errors | ✅ |

---

## 3. Quick Start

### Prerequisites

- **Windows 10/11** (required for WPF, FlaUI/UIA3, Win32 hooks, screenshots)
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- The solution compiles on Linux (via `EnableWindowsTargeting`) but only Domain tests run there

### Build

```shell
# Full solution (all 4 src projects + 4 test projects)
dotnet build

# Single layer
dotnet build src/WindowsUiFlowRecorder.Presentation
```

### Run

```shell
dotnet run --project src/WindowsUiFlowRecorder.Presentation
```

This launches the WPF application with 5 tabs:
1. **Flow Recorder** — target selection, recording controls, session summary
2. **Smart UI Scanner** — process picker, hierarchy tree, element details
3. **Session History** — list, rename, annotate, delete sessions
4. **Application Profiles** — view, edit, duplicate, delete profiles
5. **Settings** — screenshot mode, sensitivity, timeouts, export directory

### Deploy (self-contained build)

For air-gapped workstations with no .NET runtime:

```shell
# Using the included publish script
./publish.sh

# Or manually:
dotnet publish src/WindowsUiFlowRecorder.Presentation \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

The portable build is at `./publish/WindowsUiFlowRecorder.Presentation.exe`.

---

## 4. How To Use — Flow Recorder

### Step 1: Configure a Target

1. Open the **Application Profiles** tab
2. Click **⟳ Refresh** to load existing profiles (or start fresh)
3. Use the **Name** and **Desc** fields to edit a profile's metadata
4. To create a real profile, edit `%LOCALAPPDATA%\WindowsUiFlowRecorder\Profiles\{profileId}.json` directly:

```json
{
  "ProfileId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "Name": "Notepad",
  "Description": "Simple single-app test",
  "CreatedAtUtc": "2026-07-05T12:00:00.000Z",
  "LastModifiedAtUtc": "2026-07-05T12:00:00.000Z",
  "LaunchChain": {
    "Steps": [
      {
        "StepOrder": 1,
        "ApplicationTag": "Notepad",
        "ExecutablePath": "notepad.exe",
        "Arguments": null,
        "WorkingDirectory": null,
        "ReadinessCondition": {
          "ConditionType": "ProcessStarted"
        },
        "ReadinessTimeoutSecondsOverride": null,
        "CleanUpOnFailure": true
      }
    ]
  }
}
```

For a multi-app launch chain (Proxy App → eAdmin App):

```json
{
  "Steps": [
    {
      "StepOrder": 1,
      "ApplicationTag": "ProxyApp",
      "ExecutablePath": "C:\\Proxy\\ProxyApp.exe",
      "Arguments": null,
      "ReadinessCondition": {
        "ConditionType": "ControlPropertyEquals",
        "ElementAutomationId": "lblHsmStatus",
        "ExpectedPropertyName": "Value",
        "ExpectedPropertyValue": "Connected"
      },
      "ReadinessTimeoutSecondsOverride": 30,
      "CleanUpOnFailure": true
    },
    {
      "StepOrder": 2,
      "ApplicationTag": "EAdminApp",
      "ExecutablePath": "C:\\eAdmin\\eAdminApp.exe",
      "Arguments": null,
      "ReadinessCondition": {
        "ConditionType": "WindowAppeared",
        "WindowTitlePattern": "eAdmin*",
        "WindowMatchMode": "Contains"
      },
      "ReadinessTimeoutSecondsOverride": 30,
      "CleanUpOnFailure": true
    }
  ]
}
```

### Step 2: Start Recording

1. Switch to the **Flow Recorder** tab
2. Click **Start Recording** — the tool launches the target application(s) automatically
3. A blinking red overlay appears in the top-right corner: **Recording active**

### Step 3: Perform Your Test

- Click buttons, type text, switch windows — everything is captured
- The overlay blinks red while recording
- Click **Pause** to pause (overlay turns orange)
- Click **Resume** to continue

### Step 4: Stop & Review

1. Click **Stop** — the session is saved and the overlay disappears
2. A **Session Summary** appears showing: duration, action count, window count, screenshot count

### Step 5: Export

1. Click **Export** — a folder picker opens
2. Choose a destination folder
3. The tool writes:
   - `export.json` — the versioned `ExportPackage` document
   - `screenshots/` — all captured PNG images, referenced by relative path
4. The exported package is fully portable — copy it anywhere

### Step 6: Manage Sessions

1. Switch to the **Session History** tab
2. Select a session to rename, add notes, or delete
3. Re-export any completed session by selecting it and clicking Export in the Recorder tab

---

## 5. How To Use — Smart UI Scanner

1. Switch to the **Smart UI Scanner** tab
2. Select a running application from the dropdown (click **⟳ Refresh** to update the list)
3. Click **Scan** — the full UIA hierarchy tree loads in the left panel
4. **Search** — type in the search box to filter by AutomationId, Name, ControlType, or ClassName
5. **Select an element** — click any node in the tree:
   - A red highlight border appears on screen around the element
   - The right panel shows full metadata (AutomationId, bounding rectangle, patterns, etc.)
6. Click **Export Scan** to export the scan as a standalone JSON document

---

## 6. How To Configure Settings

1. Switch to the **Settings** tab
2. Adjust any setting:
   - **Screenshot Mode**: Every Action (default), Window Change Only, Manual Checkpoint Only, Off
   - **Element Cropped Screenshot**: Capture additional element-bounded screenshots
   - **Hierarchy Re-capture Sensitivity**: Low (2s), Medium (500ms), High (100ms)
   - **Default Export Directory**: Pre-fills the export dialog
   - **Readiness Timeout**: How long to wait for a launch condition (default 30s)
   - **Readiness Poll Interval**: How often to poll conditions (default 250ms)
   - **Max Hierarchy Elements**: Safety limit for tree walks (default 5,000)
   - **Verbose Diagnostic Logging**: Enable detailed logs (opt-in only)
3. Click **Save Settings** — changes take effect immediately

---

## 7. File Locations

```
%LOCALAPPDATA%\WindowsUiFlowRecorder\
├── Profiles\           # Saved ApplicationProfile JSON files (one per profile)
├── Sessions\           # Recorded session data (pre-export, one folder per session)
│   └── {sessionId}\
│       ├── session.json
│       └── screenshots\
├── Settings\           # settings.json
└── Logs\               # Rolling log files
```

Exported packages are written to the user-chosen destination and contain:
```
{export-folder}/
├── export.json          # Versioned ExportPackage document
└── screenshots/         # PNG screenshot files
```

---

## 8. Test

```shell
# Domain tests (works on any platform — 18 tests)
dotnet test tests/WindowsUiFlowRecorder.Domain.Tests

# All tests (requires Windows Desktop Runtime — 21 architecture + 14 service + 18 domain + edge cases)
dotnet test

# Architecture compliance only
dotnet test --filter "FullyQualifiedName~ArchitectureComplianceTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Architecture compliance tests (21 tests)

| Test | What it checks |
|---|---|
| `Domain_HasNoUnsolicitedDependencies` | Domain doesn't reference FlaUI, Application, Infrastructure, or Presentation |
| `Domain_HasNoNetworkDependencies` | Domain doesn't reference System.Net/Http/Sockets |
| `Application_DoesNotReferenceFlaUI` | Application is FlaUI-free |
| `Application_DoesNotReferenceWpfNamespaces` | Application is WPF-free |
| `Application_HasNoNetworkDependencies` | Application is network-free |
| `Infrastructure_ImplementsApplicationInterfaces` | Every Application abstraction has an Infrastructure implementation |
| `Infrastructure_HasNoNetworkDependencies` | Infrastructure is network-free |
| `DependencyDirection_DomainHasNoOutgoingProjectRefs` | Domain → Application → Infrastructure direction |
| `Presentation_HasNoNetworkDependencies` | Presentation is network-free |
| `AllLayers_FlaUiDoesNotLeakUpward` | No FlaUI in Domain/Application/Presentation |
| `AllLayers_StrictDependencyDirection` | Full dependency graph check |

---

## 9. Solution Structure

```
WindowsUiFlowRecorder.sln
├── src/
│   ├── WindowsUiFlowRecorder.Domain/         # Entities, Policies, Repository interfaces, Common
│   │   ├── Entities/         (14 files)
│   │   ├── Policies/         (2 files)
│   │   ├── Abstractions/     (3 repository interfaces)
│   │   └── Common/           (20 enum/record types)
│   ├── WindowsUiFlowRecorder.Application/     # Services, Interfaces, Export DTOs, DI
│   │   ├── Recording/        (RecordingSessionService + interface)
│   │   ├── Launching/        (ApplicationLaunchOrchestrator)
│   │   ├── Scanning/         (UiScanService)
│   │   ├── Export/           (ExportService, 12 DTOs)
│   │   ├── Profiles/         (ApplicationProfileService)
│   │   ├── Settings/         (SettingsService)
│   │   ├── Abstractions/     (5 infrastructure-facing interfaces)
│   │   └── DependencyInjection/
│   ├── WindowsUiFlowRecorder.Infrastructure/  # FlaUI, Win32 hooks, JSON persistence, screenshots
│   │   ├── Automation/       (FlaUiAutomationProvider)
│   │   ├── Processes/        (ProcessLaunchMonitor)
│   │   ├── Screenshots/      (ScreenshotCapturer)
│   │   ├── Input/            (GlobalInputHook)
│   │   ├── Persistence/      (3 JSON repos + ExportWriter)
│   │   ├── Logging/          (LoggingConfiguration)
│   │   └── DependencyInjection/
│   └── WindowsUiFlowRecorder.Presentation/   # WPF Views, ViewModels, DI composition root
│       ├── Recorder/         (RecorderViewModel, RecorderView)
│       ├── Scanner/          (ScannerViewModel, ScannerView, ElementTreeNode)
│       ├── Profiles/         (SessionListViewModel, ProfileManagerViewModel + Views)
│       ├── Settings/         (SettingsViewModel + View)
│       ├── Shared/           (ViewModelBase, RelayCommand, converters, overlays)
│       └── DependencyInjection/
└── tests/
    ├── WindowsUiFlowRecorder.Domain.Tests/       # 18 tests
    ├── WindowsUiFlowRecorder.Application.Tests/   # 35 tests (14 services + 11 architecture + 10 edge cases)
    ├── WindowsUiFlowRecorder.Infrastructure.Tests/ # Integration tests (requires Windows)
    └── WindowsUiFlowRecorder.Presentation.Tests/
```

---

## 10. Documentation Index

| Document | Contents |
|---|---|
| [`docs/PRD.md`](./docs/PRD.md) | Product vision, personas, goals/non-goals, requirements, acceptance criteria |
| [`docs/Architecture.md`](./docs/Architecture.md) | Clean Architecture layering, structure, dependency graph, sequence & component diagrams |
| [`docs/SystemDesign.md`](./docs/SystemDesign.md) | State machines, algorithms, threading, timing budgets, file layout, failure handling |
| [`docs/DataModel.md`](./docs/DataModel.md) | Field-level DTO/entity contracts for every model |
| [`docs/UseCases.md`](./docs/UseCases.md) | Actor flows with worked examples |
| [`docs/TestingStrategy.md`](./docs/TestingStrategy.md) | Test pyramid, mock strategy, integration harness |
| [`docs/Roadmap.md`](./docs/Roadmap.md) | Phased build order (all 10 phases completed) |
| [`docs/RiskAnalysis.md`](./docs/RiskAnalysis.md) | Risk register with mitigations |
| [`docs/FutureEnhancements.md`](./docs/FutureEnhancements.md) | Out-of-MVP ideas (AI generation, cloud sync, etc.) |
| [`docs/CodingGuidelines.md`](./docs/CodingGuidelines.md) | Naming, layering, DI, serialization, testing conventions |

---

## 11. Technology Stack

| Concern | Choice |
|---|---|
| Language | C# (.NET 8) |
| UI Framework | WPF, MVVM |
| UI Automation | FlaUI (UIA3) |
| Architecture | Clean Architecture (4 layers) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Logging | `Microsoft.Extensions.Logging` |
| Serialization | `System.Text.Json` |
| Testing | xUnit, FluentAssertions, Moq |

---

## 12. Architecture Constraints (Enforced by Tests)

| Rule | Enforced by |
|---|---|
| Domain references no other project | `Domain_HasNoUnsolicitedDependencies` |
| Application references no FlaUI/WPF | `Application_DoesNotReferenceFlaUI`, `Application_DoesNotReferenceWpfNamespaces` |
| Infrastructure implements all Application interfaces | `Infrastructure_ImplementsApplicationInterfaces` |
| Domain → Application → Infrastructure direction | `DependencyDirection_DomainHasNoOutgoingProjectRefs`, `AllLayers_StrictDependencyDirection` |
| No network calls in any layer | 4 layer-specific `*_HasNoNetworkDependencies` tests |
| No FlaUI leaks upward | `AllLayers_FlaUiDoesNotLeakUpward` |
| Nullable reference types enabled | `.editorconfig` + build errors |

---

## 13. Known Limitations (MVP)

- **UIA Events not fully wired** — `FlaUiAutomationProvider.SubscribeToEventsAsync` logs but does not attach real UIA event handlers. Window discovery relies on the capture pipeline polling the process window list. Real UIA event subscription would reduce latency for window-opened events.
- **Single-user, single-workstation** — no collaboration features
- **Windows-only** — requires Windows 10/11 Desktop Runtime
- **No built-in test runner** — the export format is the integration surface for downstream tooling
- **No edit-in-place for recorded actions** — basic rename/annotate/delete only

See [`docs/RiskAnalysis.md`](./docs/RiskAnalysis.md) for the full risk register and [`docs/FutureEnhancements.md`](./docs/FutureEnhancements.md) for post-MVP ideas.