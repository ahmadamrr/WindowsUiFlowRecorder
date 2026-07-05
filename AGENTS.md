# AGENTS.md — Windows UI Flow Recorder & Smart UI Scanner

**This is a greenfield documentation-only repo.** No source code, no build files, no CI/CD exist yet. All specs are in `docs/`.

## Source of truth hierarchy

- `docs/PRD.md` — requirements (master, resolve conflicts here first)
- `docs/Architecture.md` — layers, solution structure, dependency graph
- `docs/SystemDesign.md` — runtime behavior, threading, timing budgets
- `docs/DataModel.md` — field-level DTO/entity contracts
- `docs/CodingGuidelines.md` — naming, DI, serialization, testing conventions
- `docs/TestingStrategy.md` — test pyramid, mock strategy, integration harness
- `docs/Roadmap.md` — phased build order (Phase 0 → Phase 10)
- `docs/UseCases.md` — actor flows with worked examples
- `docs/Contracts.md` — interface method signatures, Result<T>, enums, event types

## Architecture (Clean Architecture, 4 layers)

```
WindowsUiFlowRecorder.sln
├── src/
│   ├── WindowsUiFlowRecorder.Domain/         # Entities, Policies, Abstractions (repo interfaces)
│   ├── WindowsUiFlowRecorder.Application/     # Services, orchestrators, infrastructure-facing interfaces
│   ├── WindowsUiFlowRecorder.Infrastructure/  # FlaUI, Win32 hooks, JSON persistence, screenshots
│   └── WindowsUiFlowRecorder.Presentation/   # WPF/MVVM Views + ViewModels, App.xaml.cs (DI root)
└── tests/
    ├── WindowsUiFlowRecorder.Domain.Tests/
    ├── WindowsUiFlowRecorder.Application.Tests/
    ├── WindowsUiFlowRecorder.Infrastructure.Tests/
    └── WindowsUiFlowRecorder.Presentation.Tests/
```

**Dependency direction:** Domain ← Application ← Presentation, Infrastructure → Application (implements interfaces). Presentation references Infrastructure **only** in `App.xaml.cs` composition root. Enforced by automated architecture tests.

## Key contracts

- Export DTOs (`ExportPackage`, `RecordingSessionExport`, etc.) live in `Application.Export` — versioned, frozen per schema. Internal domain models (`Domain.Entities`) are separate types and evolve freely.
- Application service interfaces (e.g. `IUiAutomationProvider`, `IProcessLaunchMonitor`) are **defined in Application**, implemented in Infrastructure. FlaUI types must never appear outside Infrastructure.
- Every service that can fail in expected ways returns `Result<T>`, not exceptions.

## Delivery order (Roadmap phases)

0. Foundation (skeleton, DI, logging, no-network test)
1. Domain entities + policies + Application interfaces
2. UIA adapter + single-app launch + test harness
3. Launch chain orchestrator + readiness conditions
4. Input capture & correlation
5. Hierarchy & screenshot capture
6. Recording session orchestration + Recorder UI
7. Export pipeline
8. Smart UI Scanner
9. Session/profile management + settings
10. Hardening & acceptance

## Test conventions

- **xUnit + FluentAssertions + Moq.** One assertion concept per test. Naming: `MethodUnderTest_Scenario_ExpectedOutcome`.
- Domain policies are pure functions — test with zero doubles. Application services mock interfaces. Infrastructure tests exercise real implementations against the Automation Test Harness.
- **Architecture compliance tests** run on every build and block merge: no-network-access, dependency-direction, no-FlaUI-leakage.
- Performance tests (timing budgets: ≤100ms per action, ≤3s hierarchy scan for ≤2000 elements) run on release candidates only.
- Automation Test Harness lives in `tools/AutomationTestHarness/` (WPF/WinForms with configurable controls, status labels, "crash now", "generate N controls" modes). Never shipped.

## Serialization (System.Text.Json)

- PascalCase property names matching `DataModel.md` exactly — no camelCase conversion
- Enums as strings via `JsonStringEnumConverter`
- Timestamps: ISO 8601 UTC with millisecond precision
- Optional fields omitted from JSON (not serialized as null)
- `ExportPackage.SchemaVersion` and `ToolVersion` are always first two properties

## DI conventions

- One `IServiceCollection` extension per layer (`AddDomainLayer` → `AddApplicationLayer` → `AddInfrastructureLayer` → `AddPresentationLayer`), called in that order in `App.xaml.cs`.
- Session-stateful services scoped per session; stateless orchestrators and Infrastructure adapters are singletons.

## Coding conventions (enforced by .editorconfig + nullable as build errors)

- Nullable reference types enabled solution-wide; nullability warnings = build errors
- Immutable data types (`record` / init-only) by default
- `_camelCase` private fields, `I`-prefixed interfaces, `Async`-suffixed async methods
- Constructor injection only — no service locator, no static DI access
- One class per file, filename matches type name
- No silent `catch` blocks; no `.Result` / `.Wait()` on async code; `async void` only in event handlers
