# Coding Guidelines
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** The final master document. Defines coding conventions for implementing the solution structure fixed in `Architecture.md` §4, the runtime behavior in `SystemDesign.md`, and the exact shapes in `DataModel.md`. These guidelines apply to the Recorder/Scanner application's own codebase (.NET 8, WPF, Clean Architecture) — not to the FlaUI test scripts a QA engineer later hand-writes against a target application like Proxy App or eAdmin App, which are a separate concern outside this repository. No source code is included here, only naming, structural, and process rules precise enough to remove ambiguity for an implementing engineer or coding agent.

---

## 1. Purpose & Scope

Every rule below exists to keep the codebase consistent with the architecture already fixed in prior documents — not to introduce new architectural decisions. Where a rule and an earlier document could be read as conflicting, the earlier document (`Architecture.md`/`SystemDesign.md`/`DataModel.md`) wins for *what* the system does, and this document wins for *how it is written*.

---

## 2. General Principles

1. **Nullable reference types are enabled solution-wide.** A type is nullable only when `DataModel.md` marks the corresponding field "Optional"; every "Required" field is a non-nullable type. This makes the data model's Required/Optional distinction directly checkable by the compiler, not just documentation.
2. **Constructor injection only.** No service locator pattern, no static singletons for anything registered in DI, no `new`-ing up a concrete Infrastructure type from Application/Presentation code — always resolve through the interface via the constructor.
3. **Immutability by default for data-carrying types.** Every model in `DataModel.md` (both Export Contract and Internal Domain families) is implemented as an immutable type (e.g., `record` or a class with init-only properties) unless a specific field is documented as mutable during recording (e.g., `RecordingSession.State`, which legitimately transitions). Mutation is the exception, justified per type, not the default.
4. **One class, one responsibility, one file.** File name matches the type name exactly; no multiple public types per file except small, tightly-coupled private/internal helper types.
5. **No implicit reliance on UI-thread affinity outside Presentation.** Any Application/Infrastructure code that must run on a specific thread (per `SystemDesign.md` §11) says so explicitly in its method-level documentation, rather than relying on an undocumented assumption about the caller.
6. **Fail loudly on architectural violations.** Prefer a compiler error or a failing architecture test (`TestingStrategy.md` §6) over a code-review-only convention wherever a rule can be mechanically enforced.

---

## 3. Solution & Project Structure Conventions

- Project and root namespace are identical for every project: `WindowsUiFlowRecorder.Domain`, `WindowsUiFlowRecorder.Application`, `WindowsUiFlowRecorder.Infrastructure`, `WindowsUiFlowRecorder.Presentation`, matching `Architecture.md` §4 exactly.
- Sub-namespaces mirror the folder structure in `Architecture.md` §4 exactly (e.g., `WindowsUiFlowRecorder.Application.Launching`, `WindowsUiFlowRecorder.Infrastructure.Automation`) — a type's namespace always tells you which architectural folder/layer it belongs to without needing to open the file.
- Test projects mirror their `src` counterpart 1:1 in both name and internal folder structure (`WindowsUiFlowRecorder.Domain.Tests` mirrors `WindowsUiFlowRecorder.Domain`, folder-for-folder), per `TestingStrategy.md` §2.2.
- The Automation Test Harness (`TestingStrategy.md` §9) lives outside `src`/`tests`, in its own `tools/AutomationTestHarness/` folder, and is excluded from production packaging (`Roadmap.md` Phase 10).

---

## 4. Naming Conventions

| Element | Convention | Example pattern |
|---|---|---|
| Interfaces | `I` + PascalCase noun/capability | `IUiAutomationProvider`, `ISessionRepository` |
| Async methods | Suffixed `Async`, always returning `Task`/`Task<T>` | `StartSessionAsync`, `FindElementAsync` |
| Private fields | `_camelCase` | `_logger`, `_sessionRepository` |
| Constants | PascalCase (not `SCREAMING_CASE`), per .NET convention | `DefaultReadinessPollIntervalMilliseconds` |
| Enums | Singular PascalCase type name; members PascalCase, matching `DataModel.md` enum value strings exactly (since these are serialized directly per `DataModel.md` §2's "enums as strings" rule) | `enum ActionType { Click, RightClick, DoubleClick, Drag, TextEntry, KeyPress, FocusChanged, WindowActivated }` |
| DTOs/entities | Exact names from `DataModel.md`, no abbreviation | `RecordedAction`, `WindowInformation`, `ElementInformation` — never shortened to `RecAction`/`WinInfo` |
| Application services | `<Capability>Service` or `<Capability>Orchestrator` matching `Architecture.md` §3.2 exactly | `RecordingSessionService`, `ApplicationLaunchOrchestrator` |
| Infrastructure adapters | `<Technology><Capability>` implementing the corresponding Application-layer interface | `FlaUiAutomationProvider : IUiAutomationProvider` |
| Test methods | `MethodUnderTest_Scenario_ExpectedOutcome` | `EvaluateCondition_ControlPropertyNeverMatches_ReturnsNotSatisfiedUntilTimeout` |

---

## 5. Layer-Specific Rules

### 5.1 Domain (`WindowsUiFlowRecorder.Domain`)
- No project reference to any package beyond the .NET base class libraries (`Architecture.md` §5). No `Microsoft.Extensions.*`, no FlaUI, no `System.IO` file-writing (reading is also avoided; Domain has no I/O at all).
- Policies (`ActionCoalescingPolicy`, `HierarchyRecapturePolicy`) are implemented as stateless classes or static classes with pure methods — same input always produces same output, no hidden dependency on wall-clock time beyond values explicitly passed in as parameters (this is what makes them deterministically unit-testable per `TestingStrategy.md` §3.1).
- Domain exceptions (used only for truly exceptional, non-expected conditions per `SystemDesign.md` §14) are named `<Reason>Exception` and derive from a single common `DomainException` base, never from a bare `Exception`.

### 5.2 Application (`WindowsUiFlowRecorder.Application`)
- Every method that can fail in an *expected* way (readiness timeout, invalid profile, export validation failure) returns a `Result`/`Result<T>`-style outcome rather than throwing (`SystemDesign.md` §14). Reserve thrown exceptions for genuinely unexpected failures (e.g., an injected dependency violating its own contract).
- Every Infrastructure-facing interface (`IUiAutomationProvider`, `IProcessLaunchMonitor`, etc.) is defined here, not in Infrastructure — Application owns the contract; Infrastructure fulfills it (`Architecture.md` §3.2/§5).
- No `using FlaUI.*`, no `using System.Windows.*` (WPF), and no direct `System.IO` file access appears in this project — if a service needs one of these, it takes the corresponding interface via constructor injection instead.
- Each DI-facing service is registered as its interface (e.g., `IRecordingSessionService`), even where `Architecture.md` names only the concrete service (`RecordingSessionService`) — an explicit interface is introduced for every Application service to keep Presentation decoupled from concrete Application types too.

### 5.3 Infrastructure (`WindowsUiFlowRecorder.Infrastructure`)
- This is the only project allowed to reference FlaUI, raw Win32 interop for input hooking, and concrete file-system/JSON-serialization code.
- Every adapter wraps and translates third-party/native exceptions into the plain `Result`/domain-exception vocabulary Application expects — a raw FlaUI or COM exception must never propagate up past the Infrastructure boundary unhandled (`SystemDesign.md` §7/§14).
- `FlaUiAutomationProvider` translates FlaUI's `AutomationElement` into `ElementInfo`/`WindowSnapshot` at the boundary of every public method — a FlaUI type must never appear in a method signature visible outside this project.
- All local JSON persistence uses `System.Text.Json` exclusively, with a shared, centrally-configured `JsonSerializerOptions` (enum-as-string conversion, consistent property naming policy — see §7) reused across all repository implementations rather than each one configuring its own options ad hoc.

### 5.4 Presentation (`WindowsUiFlowRecorder.Presentation`)
- Strict MVVM: Views contain only XAML and minimal code-behind limited to view-lifecycle wiring (e.g., attaching a `DataContext`); all interaction logic lives in ViewModels.
- ViewModels depend only on Application-layer interfaces (never concrete Application classes, never anything from Infrastructure, never FlaUI) — see §5.2's note on introducing explicit interfaces for every Application service.
- `App.xaml.cs` is the **only** file in the entire solution permitted to reference all four layers together, since it is the Composition Root (`Architecture.md` §9.1) — this exception is explicit and singular, not a precedent for any other Presentation file.
- Commands use the standard MVVM command pattern (e.g., a shared `RelayCommand`/`AsyncRelayCommand` helper type) rather than ad hoc `ICommand` implementations scattered per ViewModel.

---

## 6. Dependency Injection Conventions

- Each project exposes exactly one `IServiceCollection` extension method as its public DI entry point, named `Add<LayerName>Layer` (`AddDomainLayer` if Domain ever needs registrations, `AddApplicationLayer`, `AddInfrastructureLayer`, `AddPresentationLayer`), per `Architecture.md` §9.1.
- `App.xaml.cs` calls all four in a fixed, documented order (`AddDomainLayer` → `AddApplicationLayer` → `AddInfrastructureLayer` → `AddPresentationLayer`) reflecting the dependency graph in `Architecture.md` §5, so registration order never silently masks a missing dependency.
- Default lifetimes: Application-layer services are registered as scoped-per-session where they hold session state (e.g., `IRecordingSessionService` instances are one-per-active-session, not a process-wide singleton), and as singletons where they are stateless orchestration (e.g., `IApplicationLaunchOrchestrator` can be a singleton, since it does not itself hold session state across calls). Infrastructure adapters wrapping a single native resource (e.g., `IGlobalInputHook`) are singletons; repository implementations are singletons backed by the local file system.

---

## 7. Serialization Conventions (System.Text.Json)

- Property naming policy: exact PascalCase matching every field name as written in `DataModel.md` (e.g., `SchemaVersion`, `AutomationId`, `BoundingRectangle`) — no camelCase conversion, so the JSON on disk reads identically to this documentation.
- All enums use `JsonStringEnumConverter` (or equivalent), never the numeric default, per `DataModel.md` §2's "enums as strings" convention.
- Timestamps serialize as ISO 8601 UTC strings with millisecond precision, matching `DataModel.md` §2 exactly — never Unix epoch integers.
- `ExportPackage.SchemaVersion` and `ToolVersion` are always the first two properties emitted in `export.json` (property order is not semantically required by JSON, but is fixed here for human-readability when a tester opens the file directly).
- A field documented in `DataModel.md` as "Optional" and genuinely absent is *omitted* from the JSON (`JsonIgnoreCondition.WhenWritingNull` or equivalent), not emitted as an explicit `null`, per `DataModel.md` §2.

---

## 8. Async & Threading Conventions

- "Async all the way" — an async method never blocks on a `Task` synchronously (no `.Result`, no `.Wait()`); this is especially critical in the capture pipeline (`SystemDesign.md` §11), where a blocking call on the wrong thread can stall the input hook.
- Every method whose correct thread affinity matters per `SystemDesign.md` §11 (e.g., must run on the capture-processing thread, must never run on the input-hook thread) documents this explicitly in its XML doc comment — this is the mechanism by which `SystemDesign.md`'s threading model is kept discoverable in code, not only in documentation.
- `async void` is used only for top-level event handlers required by a framework signature (e.g., a WPF command's execute delegate); every other async method returns `Task`/`Task<T>`.
- Cancellation: any long-running Application-layer operation that can be user-cancelled (readiness polling, hierarchy scanning, export writing) accepts a `CancellationToken`, so the Presentation layer can wire a "Cancel" action without inventing an ad hoc stop-flag mechanism per feature.

---

## 9. Error Handling Conventions

- Follow `SystemDesign.md` §14 exactly: expected, recoverable conditions (readiness timeout, stale UIA element, disk write failure) are represented as typed `Result` failures with a specific reason, never as caught-and-swallowed exceptions and never as generic `catch (Exception)` blocks that lose the original cause.
- A `catch` block that does not rethrow, wrap into a `Result` failure, or log with full context is not permitted anywhere in the codebase — silent catches are treated as a defect, not a style nitpick.
- User-facing error messages (e.g., the launch-chain failure message in `UC-11`) are constructed in the Application layer (which has the contextual information — which step, which condition) and merely displayed verbatim by Presentation, rather than Presentation trying to reconstruct meaning from a generic exception.

---

## 10. Logging Conventions

- `ILogger<T>` is injected wherever logging is needed; no static logger instances.
- Log levels: `Trace`/`Debug` for capture-pipeline internals (coalescing decisions, poll cycles), `Information` for session lifecycle transitions (`SystemDesign.md` §3 state changes) and successful launch-chain completion, `Warning` for recoverable-but-notable conditions (hierarchy truncated, screenshot backpressure degradation, hook heartbeat miss), `Error` for failures surfaced to the user (launch timeout, export failure).
- Default-level logs never include captured UI text content (`ElementInformation.ValueOrText`, `RecordedAction.EnteredText`) or screenshot file contents, per `SystemDesign.md` §16 — only structural information (element `ControlType`, `AutomationId`, action type, counts) is logged by default. Verbose diagnostic logging that may include such content is gated behind `Settings.VerboseDiagnosticLoggingEnabled` and is logged at `Trace` level specifically so it is never accidentally enabled by a broadly-set minimum log level.
- Every log message concerning a specific session, action, or launch-chain step includes that entity's id (`SessionId`, `ActionId`, `ApplicationTag`) as a structured logging parameter (not string-interpolated into the message text), so log entries remain machine-filterable.

---

## 11. Testing Conventions

- Test method naming: `MethodUnderTest_Scenario_ExpectedOutcome` (see §4 table), consistently across all four test projects.
- Arrange/Act/Assert sections are visually separated (blank line between them) in every test, even short ones.
- FluentAssertions is used for all assertions (`result.Should().Be(...)`, `collection.Should().ContainSingle(...)`, etc.) rather than raw `Assert.*` calls, for consistent, readable failure messages.
- One logical assertion concept per test — a test verifying "the session transitions to Recording" does not also assert unrelated details about screenshot content; that belongs in its own test.
- Moq is used only for interfaces owned by the layer under test's dependencies, per the exact double/real-implementation split defined in `TestingStrategy.md` §5 — a test must not mix a mocked and a real implementation of the same interface within itself.

---

## 12. Documentation & Comments

- Every public type and public member in `Domain` and `Application` carries an XML doc comment (`/// <summary>`) — these two layers define the contracts everything else depends on, so they are documented most rigorously.
- Comments explain *why*, not *what* — a comment restating what the next line of code obviously does is removed in review; a comment explaining a non-obvious constraint (e.g., "must run on the capture-processing thread; see SystemDesign.md §11") is kept and encouraged.
- Every reference to a rule originating in another master document (`PRD.md` FR-x, `SystemDesign.md` §x, `DataModel.md` §x) is cited by document and section in the relevant XML doc comment or inline comment, so a future reader can trace *why* a piece of code exists back to its source requirement without needing institutional memory.

---

## 13. Static Analysis & Code Review Standards

- Nullable reference types: `Nullable` enabled solution-wide; nullability warnings are treated as build errors, not warnings, so §2 rule 1 is compiler-enforced, not just a convention.
- A shared `.editorconfig` at the solution root enforces naming conventions from §4 automatically where the tooling supports it (e.g., interface `I` prefix, private field `_camelCase`).
- The architecture-compliance tests from `TestingStrategy.md` §6 (no-network-access, dependency-direction, no-FlaUI-leakage) are merge-blocking build steps, not optional/advisory checks — a pull request cannot merge if any of them fail.
- Code review checklist (in addition to normal review) explicitly confirms: (a) no new project reference violates `Architecture.md` §5's dependency graph, (b) any new `DataModel.md`-shaped type matches its documented fields exactly, (c) any new expected-failure path returns a `Result` rather than throwing.

---

## 14. Version Control Conventions

- Branch naming: `phase/<n>-<short-description>` for work mapped to a `Roadmap.md` phase (e.g., `phase/3-launch-chain-orchestrator`), or `fix/<short-description>` for defect fixes outside phase work.
- Commit messages reference the FR/UC/risk they address where applicable (e.g., `"Implement ControlPropertyEquals readiness condition (FR-1.3, UC-02)"`), keeping the traceability established throughout this documentation set visible in source history, not only in markdown files.
- Pull requests stay scoped to a single `Roadmap.md` phase deliverable or a single defect wherever practical, so architecture-compliance test failures are easy to attribute to a specific change.

---

## 15. Canonical Type Reference

This table exists purely to remove any remaining ambiguity between the conceptual names used in `DataModel.md`/`Architecture.md` and the concrete C# type each must correspond to, so an implementing engineer or coding agent never has to guess.

| Conceptual name (DataModel.md / Architecture.md) | Concrete C# type | Project |
|---|---|---|
| `ExportPackage` | `ExportPackage` | `Application.Export` |
| `RecordingSessionExport` | `RecordingSessionExport` | `Application.Export` |
| `RecordedAction` (export form) | `RecordedAction` | `Application.Export` |
| `WindowInformation` | `WindowInformation` | `Application.Export` |
| `ElementInformation` | `ElementInformation` | `Application.Export` |
| `ScreenshotInformation` | `ScreenshotInformation` | `Application.Export` |
| `RecordingSession` (internal) | `RecordingSession` | `Domain.Entities` |
| `TargetApplicationContext` | `TargetApplicationContext` | `Domain.Entities` |
| `WindowSnapshot` | `WindowSnapshot` | `Domain.Entities` |
| `ElementInfo` | `ElementInfo` | `Domain.Entities` |
| `ApplicationLaunchChain` / `LaunchStep` / `ReadinessCondition` | Same names | `Domain.Entities` |
| `ApplicationProfile` | `ApplicationProfile` | `Domain.Entities` |
| `Settings` | `Settings` | `Domain.Entities` |
| `IUiAutomationProvider` | `IUiAutomationProvider` (impl: `FlaUiAutomationProvider`) | `Application.Abstractions` (impl in `Infrastructure.Automation`) |
| `IProcessLaunchMonitor` | `IProcessLaunchMonitor` (impl: `ProcessLaunchMonitor`) | `Application.Abstractions` (impl in `Infrastructure.Processes`) |
| `IScreenshotCapturer` | `IScreenshotCapturer` (impl: `ScreenshotCapturer`) | `Application.Abstractions` (impl in `Infrastructure.Screenshots`) |
| `IGlobalInputHook` | `IGlobalInputHook` (impl: `GlobalInputHook`) | `Application.Abstractions` (impl in `Infrastructure.Input`) |
| `ISessionRepository` | `ISessionRepository` (impl: `JsonFileSessionRepository`) | `Domain.Abstractions` (impl in `Infrastructure.Persistence`) |
| `IApplicationProfileRepository` | `IApplicationProfileRepository` (impl: `JsonFileProfileRepository`) | `Domain.Abstractions` (impl in `Infrastructure.Persistence`) |
| `ISettingsRepository` | `ISettingsRepository` (impl: `JsonFileSettingsRepository`) | `Domain.Abstractions` (impl in `Infrastructure.Persistence`) |
| `IExportWriter` | `IExportWriter` (impl: `ExportWriter`) | `Application.Abstractions` (impl in `Infrastructure.Persistence`) |
| `RecordingSessionService` | `RecordingSessionService` (interface: `IRecordingSessionService`) | `Application.Recording` |
| `ApplicationLaunchOrchestrator` | `ApplicationLaunchOrchestrator` (interface: `IApplicationLaunchOrchestrator`) | `Application.Launching` |
| `UiScanService` | `UiScanService` (interface: `IUiScanService`) | `Application.Scanning` |
| `ExportService` | `ExportService` (interface: `IExportService`) | `Application.Export` |
| `ApplicationProfileService` | `ApplicationProfileService` (interface: `IApplicationProfileService`) | `Application.Profiles` |
| `SettingsService` | `SettingsService` (interface: `ISettingsService`) | `Application.Settings` |

This table is authoritative: if any future document uses a name not listed here, that document must be corrected to match this table, not the other way around.
