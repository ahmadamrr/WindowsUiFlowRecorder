# Testing Strategy
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Defines how every layer in `Architecture.md` is verified, using the mandated stack (xUnit, FluentAssertions, Moq). It must remain consistent with the project/test-project mapping in `Architecture.md` §4, the runtime behaviors in `SystemDesign.md`, the schemas in `DataModel.md`, and the flows in `UseCases.md`. No source code is included — only strategy, structure, and pass/fail criteria.

---

## 1. Purpose & Scope

This document answers, for every component named in `Architecture.md`: *how is it tested, with what kind of test, using what test double, against what pass/fail bar?* It also defines the one piece of test infrastructure every phase in `Roadmap.md` depends on: a lightweight internal **Automation Test Harness** application that stands in for the real Proxy App / eAdmin App during automated test runs, so the suite does not require the real HSM-connected environment to execute reliably and repeatedly.

---

## 2. Testing Philosophy & Principles

1. **Test pyramid aligned to Clean Architecture layers.** Domain and Application layers (the largest, most business-logic-dense layers) carry the bulk of fast, isolated unit tests. Infrastructure carries fewer but essential integration tests against real Windows APIs/FlaUI. Presentation carries light ViewModel-logic tests; visual/manual verification covers the rest.
2. **Every `src` project has a mirrored `tests` project** (`Architecture.md` §4) — `WindowsUiFlowRecorder.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Presentation.Tests`. No test lives outside this mapping.
3. **Interfaces are the seam.** Every interface Application/Domain depends on (`IUiAutomationProvider`, `IProcessLaunchMonitor`, `IScreenshotCapturer`, `IGlobalInputHook`, the repository interfaces, `IExportWriter`) is fully mockable with Moq for unit tests, and has exactly one real implementation exercised by Infrastructure integration tests.
4. **Determinism over realism where they conflict.** Unit and most integration tests run against the Automation Test Harness (§9), not the real Proxy App/eAdmin App, so the suite is deterministic, fast, and runnable on any dev machine or build agent without HSM hardware. Real-application testing is reserved for `Roadmap.md` Phase 10 acceptance and periodic manual regression passes.
5. **The multi-application launch chain is the highest-risk area (`Roadmap.md` Phase 3, `RiskAnalysis.md`) and receives proportionally the deepest test coverage**, including deliberate failure-path tests (timeout, crash mid-chain, condition never met).

---

## 3. Unit Testing Strategy

### 3.1 Domain layer (`WindowsUiFlowRecorder.Domain.Tests`)

| Component under test | What is verified | Test double needed |
|---|---|---|
| `ActionCoalescingPolicy` | Given a sequence of raw input events (constructed as plain in-memory values, no real OS input), verifies correct grouping into `Click`/`Drag`/`TextEntry`/`KeyPress` per the rules in `SystemDesign.md` §8, including boundary cases (idle-threshold exactly reached, non-printable key ending a text-entry run, drag displacement threshold). | None — pure function, no doubles needed. |
| `HierarchyRecapturePolicy` | Given a previous fingerprint, a new fingerprint, and elapsed time since last capture, verifies the correct recapture/no-recapture decision per `SystemDesign.md` §9, including the minimum-interval throttle. | None. |
| Entity invariants (`RecordingSession`, `ApplicationLaunchChain`, `ReadinessCondition`, etc.) | Construction/validation rules from `DataModel.md` (e.g., a chain must have ≥1 step with contiguous `StepOrder`; a `ControlPresent`/`ControlPropertyEquals` condition must set at least one of `ElementAutomationId`/`ElementName`/`ElementControlType`). | None. |

**Style:** pure input → output assertions via FluentAssertions (e.g., `result.Should().BeEquivalentTo(expected)`); no I/O, no threading, sub-millisecond execution per test.

### 3.2 Application layer (`WindowsUiFlowRecorder.Application.Tests`)

| Component under test | What is verified | Test doubles (Moq) |
|---|---|---|
| `RecordingSessionService` | Correct state transitions (`SystemDesign.md` §3); correct delegation to coalescing/recapture policies as real input/UIA events arrive; correct behavior on a simulated target-application crash mid-session (`UC-08`); correct behavior when switching between two mocked `TargetApplicationContext`s (`UC-09`). | `IUiAutomationProvider`, `IGlobalInputHook`, `IScreenshotCapturer` mocked to emit scripted event sequences. |
| `ApplicationLaunchOrchestrator` | Correct step-by-step launch sequencing; correct polling/timeout/abort behavior (`UC-11`); correct "clean up on failure" behavior; correct handling of a process that exits immediately after start. | `IProcessLaunchMonitor`, `IUiAutomationProvider` mocked to simulate the Proxy App/eAdmin App scenario: mock returns "not ready" for N poll cycles then "ready," or never becomes ready (timeout path), or throws transient "stale element" errors that must not count as fatal. |
| `UiScanService` | Correct on-demand scan invocation and result shaping, independent of any `RecordingSession`. | `IUiAutomationProvider` mocked to return a canned hierarchy. |
| `ExportService` | Correct mapping from internal domain models to `DataModel.md` §4 export DTOs; correct self-validation against `SchemaVersion` before calling the writer; correct rejection of an invalid intermediate document (defensive test). | `IExportWriter` mocked to capture what would have been written, without touching disk. |
| `ApplicationProfileService`, `SettingsService` | Correct CRUD delegation and validation. | Respective repository interfaces mocked. |

**Style:** Moq is used to script realistic event sequences (e.g., "readiness condition returns false for the first 3 polls, then true") so timeout/success paths are both exercised deterministically without real wall-clock waiting — the orchestrator's poll interval and timeout are treated as injectable/configurable values in tests so a 30-second real timeout can be verified in milliseconds of test execution time.

### 3.3 Presentation layer (`WindowsUiFlowRecorder.Presentation.Tests`)

- ViewModel command-enablement logic (e.g., "Start Recording" is disabled until a valid selection/chain exists; "Resume" is only enabled while `Paused`).
- ViewModel-level mapping from Application-layer results (including `LaunchChainResult.Failure`) into user-facing messages.
- No WPF rendering is tested here (out of scope for automated unit tests); visual verification is manual, per `Roadmap.md` Phase 6/10.

---

## 4. Integration Testing Strategy

Integration tests exercise real Infrastructure implementations against the **Automation Test Harness** (§9) rather than mocks, and live in `WindowsUiFlowRecorder.Infrastructure.Tests` unless otherwise noted.

| Scenario | What is verified | Harness feature used |
|---|---|---|
| Single-process launch & element lookup | `ProcessLaunchMonitor` + `FlaUiAutomationProvider` can launch a harness instance, find a known `AutomationId`, and read its properties correctly. | Harness's static control set (§9.2). |
| Full hierarchy walk | Walker correctly captures a window with a known, pre-counted number of descendant elements, and respects `MaxHierarchyElementCount` truncation when exceeded. | Harness's "generate N controls" mode. |
| Structural change detection | A harness action that adds/removes controls at runtime triggers the expected recapture decision end-to-end (not just the policy in isolation, per §3.1, but wired through the real UIA adapter). | Harness's "mutate UI" button. |
| **2-step launch chain (Proxy App / eAdmin App stand-in)** | Two harness instances configured as a chain: instance A's status label must reach a configured text value before instance B launches. Verifies the full real path: process start → real UIA polling → real process start of the second instance → context tagging. | Harness's configurable status label (§9.2), toggled via a harness control or timed auto-change. |
| **Launch chain timeout/abort (real path)** | Same 2-step configuration, but instance A's status label is never changed → verifies real abort, real "clean up on failure" process termination, and the exact error content surfaced. | Harness's status label left at its default/never-changed value. |
| Input capture → correlation → coalescing (real path) | Synthetic input generation (OS-level `SendInput`-equivalent) against a running harness instance produces the expected coalesced `RecordedAction`s. | Harness's known control layout. |
| Screenshot capture | Full/window/element-scoped screenshots are written as valid PNG files of the expected dimensions. | Harness's fixed-size, known-position window. |
| Crash handling (real path) | Killing a harness process mid-session is detected, the context is marked terminated, and (with a second harness instance still running) recording continues per `UC-09`. | Harness's built-in "crash now" test control (a button that calls a hard process-exit, simulating an unhandled crash rather than a graceful close). |
| Export round-trip | A full session recorded against harness instances, once exported, produces `export.json` that validates against the current schema and whose screenshot references resolve correctly when the export folder is copied to a different path. | Full harness-driven session. |
| Repository persistence | `JsonFileSessionRepository`, `JsonFileProfileRepository`, `JsonFileSettingsRepository` correctly round-trip real objects to/from the local app-data folder structure (`SystemDesign.md` §12). | N/A (no harness needed). |

Cross-layer, end-to-end integration tests (spanning Application + Infrastructure together) live in a dedicated `EndToEnd` test category within `WindowsUiFlowRecorder.Application.Tests`, since they exercise real orchestration logic against real Infrastructure rather than either in isolation.

---

## 5. Mocking & Test Doubles Strategy

| Interface | Unit-test double | Integration-test double |
|---|---|---|
| `IUiAutomationProvider` | Moq — scripted `FindElement`/`WalkHierarchy`/property-read results | Real `FlaUiAutomationProvider` against the Automation Test Harness |
| `IProcessLaunchMonitor` | Moq — scripted start/exit/window-enumeration results | Real `ProcessLaunchMonitor` launching real harness processes |
| `IScreenshotCapturer` | Moq — verifies correct calls/parameters, no real image produced | Real `ScreenshotCapturer` writing real PNG files to a temp test directory |
| `IGlobalInputHook` | Moq — raises scripted raw input events | Real `GlobalInputHook` driven by synthetic OS input in a small number of high-value integration tests only (real global hooks are the most environment-sensitive component; kept to a minimal, well-isolated set of tests) |
| `ISessionRepository`, `IApplicationProfileRepository`, `ISettingsRepository` | Moq (Application-layer tests) | Real JSON-file implementations against a temp directory (Infrastructure-layer tests) |
| `IExportWriter` | Moq — captures the DTO graph that would be written | Real writer, asserting on actual files produced in a temp directory |

**Rule of thumb:** if a test's purpose is to verify *orchestration logic* (sequencing, state transitions, error propagation), it mocks. If a test's purpose is to verify that a *real Windows/FlaUI/file-system interaction behaves as SystemDesign.md specifies*, it uses the real implementation against the harness or a temp directory — never both mocked and real in the same test.

---

## 6. Architecture Compliance Testing

- **No-network-access test** (`Architecture.md` §9.3): an automated test scans the compiled output/project references of every project for disallowed networking namespaces/packages (`System.Net.*`, `HttpClient`, etc.) and fails if any are found outside an explicitly reviewed allow-list (empty in MVP). This test runs on every build and is a merge-blocking gate (§11).
- **Layering/dependency-direction test**: an automated test (e.g., reflection-based project-reference assertion) verifies that `Domain` has no outgoing project references, `Application` references only `Domain`, and `Presentation`/`Infrastructure` never leak the wrong direction — enforcing `Architecture.md` §5's dependency graph as a checked fact, not just a diagram.
- **No-FlaUI-type-leakage test**: a targeted test verifies that no public member of any Application/Domain-layer type exposes a FlaUI namespace type, keeping the automation-provider abstraction (`Architecture.md` §1 principle 3) honest over time.

---

## 7. Performance Testing Strategy

Performance tests are a distinct suite (not run on every commit; see §11) validating the budgets defined in `SystemDesign.md` §13, on the documented reference hardware baseline.

| Budget | Test approach |
|---|---|
| ≤100ms added input latency per captured action | Automated timing harness measures elapsed time from a synthetic input event reaching the hook to the corresponding `RecordedAction` being appended, across a statistically meaningful number of repetitions (e.g., 500 actions), asserting on the 95th percentile, not just the mean. |
| ≤3s full hierarchy scan for ≤2,000 elements | Automation Test Harness's "generate N controls" mode set to 2,000; scan is timed end-to-end through `UiScanService`. |
| Memory ceiling over a 30-minute session with screenshots on | A scripted long-running session against the harness (accelerated input generation, not a real 30-minute manual pass) with periodic memory snapshots, asserting the ceiling is not exceeded and that memory is not monotonically growing in a way that indicates a leak (e.g., comparing the growth rate over the second half of the run to the first half). |
| Readiness poll responsiveness | Verifies the orchestrator detects a harness status-label change within one poll interval (250ms default) of it occurring, not just "eventually." |

Performance test failures do not block every commit (they are slower and more environment-sensitive) but do block release candidates per `Roadmap.md` Phase 10.

---

## 8. Regression Testing Strategy

1. **Full Acceptance Criteria re-run.** Every item in `PRD.md` §13 is expressed as an automated or scripted-manual regression test and re-run before each internal release, not only once during initial development.
2. **Golden export comparison.** A fixed, scripted harness-driven session (deterministic actions, deterministic harness UI) is recorded and exported on each release candidate; the resulting `export.json` is compared against a stored "golden" reference using a structural/property-level comparison that ignores expected-to-vary fields (`ExportedAtUtc`, all GUIDs, timestamps) but asserts on everything else (action types, sequence, element metadata, hierarchy shape) — catching unintended regressions in capture or export-mapping logic.
3. **Launch-chain regression suite.** Because the launch chain is the highest-risk, most product-differentiating subsystem (`RiskAnalysis.md`), its integration tests (§4) — success path, timeout/abort path, crash-mid-chain path — are run on every build, not deferred to a periodic regression pass.
4. **Schema-version regression.** Whenever `DataModel.md` changes, a test confirms the new `SchemaVersion` is bumped appropriately (§9 of `DataModel.md`'s versioning policy) and that a previously-produced export at the old schema version can still be read/recognized (even if only to report "old schema, please re-export"), preventing silent breakage for existing exported data.
5. **Real-application spot-check.** Periodically (at minimum, before each `Roadmap.md` Phase 10 release gate), the full manual test pass is additionally run once against the *real* Proxy App and eAdmin App (not just the harness), to catch any harness/real-app behavioral drift.

---

## 9. Automation Test Harness Design

To make the above deterministic and CI-friendly, a small internal-only WPF (or WinForms, to also validate cross-technology UIA support) application is built and maintained alongside the product, never shipped to end users.

### 9.1 Purpose
Stand in for Proxy App and eAdmin App in all automated tests, so the suite never depends on real HSM hardware or the real target applications being installed on a build agent.

### 9.2 Required harness features

| Feature | Supports testing of |
|---|---|
| A status label control with a known `AutomationId` whose text can be changed via a harness-internal timer or a harness control | `ControlPropertyEquals` readiness conditions (the HSM-connected scenario), coalescing of text-entry into that label if made editable |
| A "generate N controls" mode building a configurable, known-count control tree | Hierarchy walker correctness and performance budgets |
| A "mutate UI" control that adds/removes elements at runtime | Structural-change/recapture detection |
| A "crash now" control that terminates the process abnormally (not a graceful `Close()`) | Crash-handling (`UC-08`) integration tests |
| A known, fixed-size, fixed-position window | Deterministic screenshot dimension/position assertions |
| Command-line launch arguments to pre-configure the above (e.g., start already at N controls, start with the status label pre-set) | Reduces per-test setup steps; supports the 2-step chain tests needing two differently-configured instances |
| A second, independently launchable instance/executable configuration to simulate the Dependent application in chain tests | The 2-step launch chain integration tests (§4) |

### 9.3 Ownership & maintenance
The harness lives in its own solution folder (e.g., `tools/AutomationTestHarness/`), is built and versioned alongside the product, and is explicitly excluded from the "no network access" and production-packaging steps of `Roadmap.md` Phase 10, since it is a test tool, not a shipped artifact.

---

## 10. Coverage Goals

| Layer | Target line/branch coverage | Rationale |
|---|---|---|
| Domain | ≥ 90% | Pure logic, cheapest to test exhaustively; every policy edge case is expected to be covered. |
| Application | ≥ 85% | Business-critical orchestration; mocked doubles make high coverage achievable without real I/O. |
| Infrastructure | ≥ 60% line coverage, but every public interface method has at least one integration test | Native/UIA/OS interop is harder to exhaustively branch-cover through mocks alone; integration tests against the harness are the primary confidence signal here, not raw coverage percentage. |
| Presentation | ≥ 50%, focused on ViewModel logic only | XAML/visual layout is not meaningfully unit-testable; manual verification covers the remainder. |

Coverage percentage is a supporting signal, not the primary release gate — the Acceptance Criteria (§8.1) and the architecture-compliance tests (§6) are the actual release gates.

---

## 11. CI / Build Gate Policy

| Gate | When it runs | Blocks merge/release? |
|---|---|---|
| Domain + Application unit tests | Every build/commit | Yes |
| Architecture compliance tests (§6) | Every build/commit | Yes |
| Infrastructure integration tests (harness-based) | Every build/commit | Yes |
| Presentation unit tests | Every build/commit | Yes |
| Performance tests (§7) | Nightly / on-demand, and mandatorily before a release candidate | Blocks release candidates only, not every commit |
| Golden export comparison (§8.2) | On-demand and before each release candidate | Blocks release candidates |
| Real-application spot-check (§8.5) | Before each `Roadmap.md` Phase 10 release gate | Blocks release |

---

## 12. Test Data & Fixtures

- Deterministic object-builder helpers (conceptually, "object mothers") construct valid `RecordingSession`, `ApplicationLaunchChain`, `ReadinessCondition`, and other `DataModel.md` entities for unit tests, so tests do not hand-construct large nested objects inline and stay readable.
- A small library of canned harness configurations (e.g., "2-step chain, condition met after 3 polls," "2-step chain, condition never met") is reused across `ApplicationLaunchOrchestrator` unit tests and their corresponding real integration-test counterparts, keeping the two test levels conceptually aligned even though one mocks and the other runs for real.

---

## 13. Traceability Summary

| Testing activity | Validates |
|---|---|
| Domain/Application unit tests | `PRD.md` FR-1 through FR-9 logic correctness in isolation |
| Infrastructure integration tests | `SystemDesign.md` §4–§10 real-world runtime behavior |
| Architecture compliance tests | `Architecture.md` §1 principles 1–3, and the Offline-operation NFR |
| Performance tests | `SystemDesign.md` §13 budgets / `PRD.md` NFRs |
| Golden export + schema regression | `DataModel.md` §9 versioning policy, `PRD.md` NFR "Data integrity" |
| Launch-chain regression suite + real-app spot-check | `PRD.md` Success Metric (≥95% launch-chain success rate), `UseCases.md` UC-02/UC-04/UC-09/UC-11 |
| Full Acceptance Criteria re-run | `PRD.md` §13, in full, before every release |
