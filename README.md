# Windows UI Flow Recorder & Smart UI Scanner

> Offline Windows desktop tool that records manual QA testing sessions against Windows desktop applications (via FlaUI/UIA3) and exports structured JSON containing UI Automation metadata, screenshots, and window hierarchy — eliminating the manual Inspect.exe / AutomationId-hunting workflow for FlaUI test authors.

---

## 1. What This Project Is

QA engineers who automate Windows desktop applications with **FlaUI** currently spend a large portion of their time doing manual, repetitive discovery work: opening `Inspect.exe`, clicking through a UI hierarchy, copying `AutomationId`s and `ControlType`s by hand, and then hand-writing Page Objects and FlaUI scripts.

This project replaces that manual discovery loop with two integrated capabilities:

| Component | Purpose |
|---|---|
| **Flow Recorder** | Attaches to a running target application (or launches one), watches the tester perform a manual test case, and records every user action (clicks, keystrokes, focus changes) together with full UI Automation metadata for the control involved, a screenshot, and the active window's automation tree snapshot. |
| **Smart UI Scanner** | On demand, walks the full UI Automation tree of a selected window/application and exports a structured snapshot of every control, its properties, and its hierarchy — independent of any recording session. |

The output of both components is **structured JSON**, designed to be the deterministic, machine-readable input for a *future* AI-assisted Page Object / FlaUI code generator. **No AI, cloud, or network dependency is part of this project.** Everything runs fully offline on the tester's Windows machine.

This documentation set is the **single source of truth** for the system. Every other document, and any future implementation work (human or AI-agent-driven), must be consistent with what is defined here.

---

## 2. Why This Exists (Problem Context)

Our QA organization builds FlaUI-based UI automation (C#, Visual Studio, UIA3) against multi-process Windows desktop products. A representative real-world case: a product suite consisting of a **Proxy App** that must be launched first to establish a connection to a **Hardware Security Module (HSM)**, followed by an **eAdmin App** that depends on that Proxy connection being alive. Testing and automating flows like this requires a tester to:

1. Manually execute the test case end-to-end (launch Proxy → confirm HSM connection → launch eAdmin → perform actions).
2. Separately re-discover every control touched, using `Inspect.exe`, across **both** application processes.
3. Hand-assemble Page Objects and FlaUI code from notes and screenshots.

This is slow, error-prone, and does not scale as the number of flows and applications grows. The Flow Recorder and Smart UI Scanner directly target this workflow, including multi-process flows where recording must survive the tester switching between application windows/processes mid-session (see `UseCases.md`, UC-09).

---

## 3. Documentation Index

| Document | Contents |
|---|---|
| [`PRD.md`](./PRD.md) | Product vision, personas, goals/non-goals, functional & non-functional requirements, release plan, acceptance criteria |
| [`Architecture.md`](./Architecture.md) | Clean Architecture layering, solution/project structure, dependency graph, data flow, sequence & component diagrams |
| [`SystemDesign.md`](./SystemDesign.md) | Detailed subsystem design: recording engine, event capture pipeline, UIA tree walker, screenshot service, export pipeline, state machines |
| [`DataModel.md`](./DataModel.md) | Every shared DTO/model: properties, types, responsibilities, relationships |
| [`UseCases.md`](./UseCases.md) | Actor-level use cases with preconditions, main/alternate flows, postconditions |
| [`TestingStrategy.md`](./TestingStrategy.md) | Unit/integration/mocking/performance approach, coverage goals, regression strategy |
| [`Roadmap.md`](./Roadmap.md) | Phased delivery plan with goals, deliverables, dependencies, complexity, risks per phase |
| [`RiskAnalysis.md`](./RiskAnalysis.md) | Risk register: technical, product, and operational risks with mitigations |
| [`FutureEnhancements.md`](./FutureEnhancements.md) | Explicitly out-of-scope-for-MVP ideas (AI generation, cloud sync, self-healing locators, etc.) |
| [`CodingGuidelines.md`](./CodingGuidelines.md) | Naming conventions, layering rules, DI/logging/testing conventions, review checklist |

---

## 4. Technology Stack (Authoritative)

| Concern | Choice |
|---|---|
| Language | C# (latest, aligned to .NET 8) |
| Runtime | .NET 8 |
| UI Framework | WPF, MVVM pattern |
| UI Automation | FlaUI (UIA3 backend) |
| Architecture Style | Clean Architecture |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` |
| Logging | `Microsoft.Extensions.Logging` |
| Serialization | `System.Text.Json` |
| Unit/Integration Testing | xUnit, FluentAssertions, Moq |

> Note: this stack (.NET 8 / WPF tool) is the stack for the **recorder/scanner tool itself**. It is independent of, and does not replace, the tester's existing FlaUI test projects (which may target .NET Framework 4.8, as is common for legacy enterprise desktop test suites). The recorder is a standalone authoring tool whose *output* (JSON) is designed to be consumable from any FlaUI project regardless of target framework.

---

## 5. MVP Scope Boundary

**In scope (MVP):**
- Flow Recorder (attach/launch target, record actions, capture metadata/screenshots/hierarchy, export JSON)
- Smart UI Scanner (on-demand full tree snapshot export)
- Fully offline operation
- Structured JSON export as the terminal artifact

**Explicitly out of scope (MVP):** AI-assisted code generation, cloud sync/storage, team collaboration features, self-healing locators, cross-machine session replay. See `FutureEnhancements.md`.

---

## 6. How to Read This Documentation Set

Start with `PRD.md` to understand *why* and *for whom*. Read `Architecture.md` and `SystemDesign.md` to understand *how the system is structured*. Read `DataModel.md` before touching any export/serialization logic — it is the contract the whole system is built around. Read `UseCases.md` and `TestingStrategy.md` before implementing or testing any feature. `Roadmap.md` and `RiskAnalysis.md` frame delivery sequencing and known risk areas. `CodingGuidelines.md` governs all code review.

---

## 7. Build, Run & Test

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Windows 10/11** with .NET 8 Desktop Runtime (for running — FlaUI, WPF, Win32 hooks, screenshots require the Windows Desktop runtime)
- The solution compiles on Linux (via `EnableWindowsTargeting`) but tests targeting `net8.0-windows` require Windows to execute. Domain tests (`net8.0`) run on any platform.

### Build

```shell
# Restore and build all 8 projects (4 src + 4 tests)
dotnet build

# Build a single layer
dotnet build src/WindowsUiFlowRecorder.Domain
dotnet build src/WindowsUiFlowRecorder.Application
dotnet build src/WindowsUiFlowRecorder.Infrastructure
dotnet build src/WindowsUiFlowRecorder.Presentation
```

### Run

```shell
# Run the console app — verifies DI wiring and all layer services load
dotnet run --project src/WindowsUiFlowRecorder.Presentation
```

On Windows, replace the Presentation layer's `ConsoleApp` entry point with the WPF shell (`MainWindow.xaml` + `App.xaml`) and add `FlaUI.UIA3` to the Infrastructure project for real automation support.

### Test

```shell
# Run Domain tests (works on any platform - 14 tests)
dotnet test tests/WindowsUiFlowRecorder.Domain.Tests

# Run all tests (requires Windows Desktop Runtime)
dotnet test

# Run Infrastructure integration tests (requires Windows + Automation Test Harness)
dotnet test tests/WindowsUiFlowRecorder.Infrastructure.Tests

# Run with verbose output
dotnet test --verbosity detailed
```

**Platform note:** Domain tests (`net8.0`) run on Linux and Windows.  
Projects targeting `net8.0-windows` (Application.Tests, Infrastructure.Tests, Presentation.Tests) require the Windows Desktop Runtime to execute, but compile on any platform via `EnableWindowsTargeting`.

### Solution structure

```
WindowsUiFlowRecorder.sln
├── src/
│   ├── WindowsUiFlowRecorder.Domain/         # Entities, Policies, Value Objects, Repository interfaces
│   ├── WindowsUiFlowRecorder.Application/     # Service interfaces & implementations, Export DTOs
│   ├── WindowsUiFlowRecorder.Infrastructure/  # FlaUI, Win32 hooks, JSON persistence, screenshots
│   └── WindowsUiFlowRecorder.Presentation/    # App entry point, DI composition root
└── tests/
    ├── WindowsUiFlowRecorder.Domain.Tests/      # 14 tests (policies, entities, results)
    ├── WindowsUiFlowRecorder.Application.Tests/  # 14 tests (services, architecture compliance)
    ├── WindowsUiFlowRecorder.Infrastructure.Tests/
    └── WindowsUiFlowRecorder.Presentation.Tests/
```

### Architecture constraints (verified by tests)

| Rule | Enforced by |
|---|---|
| Domain references no other project | `ArchitectureComplianceTests.Domain_HasNoUnsolicitedDependencies` |
| Application references no FlaUI/WPF types | `ArchitectureComplianceTests.Application_DoesNotReferenceFlaUI` |
| Infrastructure implements all Application interfaces | `ArchitectureComplianceTests.Infrastructure_ImplementsApplicationInterfaces` |
| Domain → Application → Infrastructure dependency direction | `ArchitectureComplianceTests.DependencyDirection_DomainHasNoOutgoingProjectRefs` |

### Deployment

The tool is distributed as a **self-contained .NET 8 build** (bundling the runtime, per `RiskAnalysis.md` R-18) so no separate .NET installation is required on air-gapped test workstations:

```shell
dotnet publish src/WindowsUiFlowRecorder.Presentation \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

### Usage

The MVP currently ships as a **console application** that validates the architecture (DI wiring, service loading, repository initialization). The full WPF UI (Recorder controls, Scanner tree view, overlay) is scaffolded but not wired into the entry point yet — that happens in `Roadmap.md` Phase 6.

#### 1. Verify the build

```shell
dotnet run --project src/WindowsUiFlowRecorder.Presentation
```

Expected output:
```
info: ConsoleApp[0] Windows UI Flow Recorder & Smart UI Scanner v0.1.0
info: ConsoleApp[0] RecordingSessionService loaded - current state: Idle
info: ConsoleApp[0] ApplicationProfileService loaded - profiles count: 0
info: ConsoleApp[0] Settings loaded - screenshot mode: EveryAction, timeout: 30s
info: ConsoleApp[0] Build complete. Ready for deployment on Windows 10/11.
```

This confirms all 4 layers load and DI resolves correctly.

#### 2. Run tests

```shell
dotnet test
```

All 30 tests should pass: 14 Domain tests (entities, policies, results), 14 Application tests (mocked services, architecture compliance), 2 Infrastructure/Presentation smoke tests.

#### 3. Customize and extend

This is a **greenfield codebase** — no production-ready UI exists yet. To build the full product, follow `docs/Roadmap.md`:

| Phase | What you build |
|---|---|
| Phase 0 | Solution skeleton (done) |
| Phase 1 | Domain entities + Application contracts (done) |
| Phase 2 | Real `FlaUiAutomationProvider` — replace `Infrastructure/Automation/FlaUiAutomationProvider.cs` stub with FlaUI-backed implementation |
| Phase 3 | `ApplicationLaunchOrchestrator` readiness polling against real processes |
| Phase 4 | Real `GlobalInputHook` — replace Win32 hook stub |
| Phase 5 | Real `ScreenshotCapturer` — replace `System.Drawing` stub |
| Phase 6 | WPF Recorder UI (Views + ViewModels on `WindowsUiFlowRecorder.Presentation`) |
| Phase 7 | Export pipeline (done at Application layer; wire `ExportService` to UI) |
| Phase 8 | Smart UI Scanner UI (WPF tree view + search) |
| Phase 9 | Session list, profile manager, settings UI |
| Phase 10 | Real-app acceptance testing, performance validation, self-contained publish |

Each phase's deliverables, test expectations, and risks are detailed in `docs/Roadmap.md`.

#### 4. Connect to a real target application (Phase 2+)

Once the FlaUI adapter is implemented:

```csharp
// Example: programmatic use of the orchestrator
var orchestrator = serviceProvider.GetRequiredService<IApplicationLaunchOrchestrator>();
var chain = new ApplicationLaunchChain([
    new LaunchStep(1, "ProxyApp", @"C:\Proxy\ProxyApp.exe", null, null,
        new ReadinessCondition(ConditionType.WindowAppeared, "Proxy*", null,
            null, null, null, null, null, null, null),
        null, true)
]);
var result = await orchestrator.ExecuteLaunchChainAsync(chain, 250, CancellationToken.None);
```

#### 5. File locations (app data)

Working files are stored under:

```
%LOCALAPPDATA%\WindowsUiFlowRecorder\
├── Profiles\        # Saved ApplicationProfile JSON files
├── Sessions\        # Recorded session data (pre-export)
├── Settings\        # settings.json
└── Logs\            # Rolling log files
```
