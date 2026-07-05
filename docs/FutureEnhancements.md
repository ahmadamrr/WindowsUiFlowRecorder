# Future Enhancements
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Catalogs ideas explicitly excluded from MVP (`PRD.md` §4.1 Non-Goals) or identified as natural extensions during design (`Architecture.md` §9.4/§9.5, `SystemDesign.md` §9, `RiskAnalysis.md` residual risks). Nothing in this document authorizes implementation — any enhancement selected for a future release must first be reflected back into `PRD.md` (Goals/FRs) and `Architecture.md` (if it affects structure) before work begins, per the intake process in §7.

---

## 1. Purpose & Scope

MVP deliberately ships only the Flow Recorder and Smart UI Scanner, fully offline, with no AI (`PRD.md` §4.1). This document exists so that ideas raised during design are not lost, and so that future prioritization has a single place to start from rather than re-litigating "should we build X" from nothing. Every entry below traces back to something already named in an existing document — this document invents no new unreferenced concepts.

---

## 2. Guardrails for Any Future Enhancement

Before any item below is scheduled, it must satisfy all of the following, inherited from the MVP's non-negotiable constraints:

1. **Offline-by-construction is not silently relaxed.** Any enhancement that appears to need a network call (e.g., a cloud-hosted AI model) must either be redesigned to run fully locally, or must go through an explicit, visible decision to amend `PRD.md` §9 (Constraints) — it can never be added as a quiet exception to the existing "no network access" architecture test (`Architecture.md` §9.3, `TestingStrategy.md` §6).
2. **The Domain/Application boundary is not bypassed for convenience.** A future feature consuming `ExportPackage` data (e.g., AI-assisted generation) should be built as a separate consumer of the stable export contract, not by reaching into Domain/Application internals (`Architecture.md` §9.5).
3. **`DataModel.md`'s versioning policy governs any shape change.** An enhancement that needs new export fields is a MINOR schema version at most; one that changes existing field meaning is a MAJOR version, per `DataModel.md` §9.

---

## 3. Near-Term (Fast-Follow) Enhancements

These extend MVP without changing its architecture and are the most natural candidates for the first post-MVP iteration.

| ID | Enhancement | Motivation / Origin | Complexity | Notes |
|---|---|---|---|---|
| FE-01 | **Explicit pre-flight elevation-mismatch check and UI warning** | `RiskAnalysis.md` R-03 (Critical) — an elevated Proxy App/eAdmin App silently blocks UIA/input capture via Windows UIPI | Low–Medium | Given its Critical severity, this is the single strongest candidate to pull *forward* into the MVP hardening phase (`Roadmap.md` Phase 10) rather than deferring — listed here as a fallback if it does not make the MVP cut |
| FE-02 | **Automatic sensitive-content redaction/masking** in screenshots and captured `Value`/`Text` fields (e.g., configurable field-name-based masking, or masking any control matching a `PasswordChar` UIA property) | `RiskAnalysis.md` R-14, `DataModel.md` §10 | Medium–High | Even a partial solution (masking obvious password fields via UIA's own password-flag property) meaningfully reduces exposure without needing content-aware redaction |
| FE-03 | **Recorder self-crash recovery** — periodic best-effort autosave of the in-progress session to the working session file, with a "recover last session" prompt on next launch | `RiskAnalysis.md` R-12 | Low–Medium | Complements the existing target-application crash handling (`UC-08`) by covering the one crash scenario MVP does not address: the Recorder's own process |
| FE-04 | **Versioned hierarchy history per window**, instead of MVP's single current-snapshot-per-window model | `SystemDesign.md` §9 (explicitly flagged as a candidate there) | Medium | Useful for diagnosing UI state changes over the course of a long session, at the cost of higher memory/export size — would need its own Settings toggle to stay opt-in |
| FE-05 | **Composite readiness conditions** (AND/OR combinations of multiple `ControlPresent`/`ControlPropertyEquals` checks for a single launch step) | `RiskAnalysis.md` R-05/R-06 — reduces single-point-of-failure brittleness in readiness detection | Medium | E.g., requiring *both* a status label's text and a connection-icon control's state to indicate the Proxy App is truly HSM-connected, not just one signal |

---

## 4. Mid-Term Enhancements

Larger features that extend the product's usefulness beyond a single tester's workstation, still fully consistent with the offline constraint.

| ID | Enhancement | Motivation / Origin | Complexity | Notes |
|---|---|---|---|---|
| FE-06 | **Local network-share-based profile/session sharing** — teams store `ApplicationProfile`s (and optionally sessions) on a shared local file server rather than only per-workstation `%LOCALAPPDATA%` | `PRD.md` §4.1 explicitly excludes multi-user features from MVP; this is the lightest-weight step toward team collaboration without introducing any cloud dependency | Medium | Still zero internet access — a shared local/LAN file path is not "the cloud" and does not violate `Architecture.md` §9.3 as long as no external network boundary is crossed |
| FE-07 | **CI/test-runner integration** — export a ready-to-run xUnit/FlaUI test project skeleton (not just JSON), or a plugin consuming `ExportPackage` directly in a build pipeline | `PRD.md` §4.1 Non-Goals explicitly excludes this from MVP ("does not replace" a test runner) | High | A natural second step after AI-assisted generation (FE-11) exists, since a generated Page Object needs somewhere to run |
| FE-08 | **Session/test-case drift detection** — re-run the Smart UI Scanner against the same window captured in an older session and diff the two hierarchies, flagging controls that disappeared, moved, or changed `AutomationId` | Emerges naturally from having both `UiScanService` and stored `WindowInformation` already in MVP | Medium | Directly mitigates `RiskAnalysis.md` R-05 (readiness-condition brittleness) and helps automation engineers proactively catch UI changes before they break existing FlaUI scripts |
| FE-09 | **Additional automation-provider support** beyond UIA3 (e.g., MSAA fallback for very old Win32 controls with poor UIA support, or specialized handling for embedded web/Electron content inside a desktop shell) | `RiskAnalysis.md` R-01/R-04 | High | Would be implemented entirely behind the existing `IUiAutomationProvider` interface (`Architecture.md` §3.2/§9.5) — no Application/Domain change required, only a richer Infrastructure implementation or a provider-selection strategy |
| FE-10 | **Visual regression / screenshot diffing** between two recordings of the same manual test case, to flag unexpected visual changes independent of UIA metadata | Natural extension of existing screenshot capture (FR-5) | Medium | Complements FE-08's structural diffing with a pixel-level signal |

---

## 5. Long-Term / Visionary Enhancements

### FE-11 — AI-Assisted Page Object & FlaUI Test Script Generation (Flagship)

This is the enhancement the entire MVP was explicitly architected to enable (`Background`, `Architecture.md` §1 principle 5, §9.5) without being part of it.

**Concept:** a separate consumer application/module reads one or more `ExportPackage` documents and generates draft C# FlaUI Page Object classes and/or xUnit test method skeletons, using the captured `ElementInformation` (AutomationId, ControlType, hierarchy path) and `RecordedAction` sequence as its input — replacing the remaining manual step of translating a structured export into hand-written automation code.

**Why it is deferred, not MVP:** `PRD.md` §4.1 explicitly excludes it; it also introduces the project's only plausible reason to consider an AI/LLM component, which directly conflicts with the "no AI, no cloud" MVP mandate unless resolved per the guardrail in §2.1 above.

**Architectural fit already in place:**
- `ExportPackage` (`DataModel.md` §4) is the intended, stable integration surface — this future module would consume exported JSON, never reach into Domain/Application/Infrastructure.
- `Architecture.md` §9.5 already states this explicitly as a design goal.
- `DataModel.md` §9's semver policy means this future consumer can be built to target a known schema version without needing the capture engine to change.

**Open design questions for when this is scoped:**
- **Offline model constraint:** per §2.1, this must either run a locally-hosted model (no outbound calls) or require an explicit, visible amendment to the product's offline constraint — it cannot be added as a quiet exception.
- **Scope of generation:** fully-generated, ready-to-run Page Objects vs. a scaffolded draft requiring human review — the latter is the safer initial target given FlaUI selector correctness has real test-reliability consequences.
- **Feedback loop:** whether corrections a human makes to generated code should ever flow back to improve future generation (a significant scope increase, likely a separate future item rather than part of FE-11 itself).

### FE-12 — Natural-Language Session Annotation

**Concept:** while recording, a tester optionally narrates test intent in plain language (e.g., "now verifying the connection status turns green"), captured via a fully local/offline speech-to-text component and attached to the nearest `RecordedAction`(s) as additional context for later automation authoring or for FE-11's generation step.

**Constraint:** must use a locally-hosted speech-to-text model per §2.1 — no cloud transcription service.

### FE-13 — Self-Healing Selector Suggestions

**Concept:** using the structural-diff signal from FE-08, suggest more resilient selector strategies (e.g., "prefer this stable ancestor + relative position over this AutomationId, which has changed twice across recorded sessions") to reduce the readiness-condition and Page-Object brittleness identified in `RiskAnalysis.md` R-01/R-05.

### FE-14 — Cross-Platform / Additional Automation Framework Support

**Concept:** extending capture beyond Windows Desktop (e.g., web UI via Playwright/Selenium, or non-Windows desktop UI).

**Status:** explicitly a Non-Goal for the foreseeable product direction (`PRD.md` §4.1) — listed here only for completeness, not because it is currently anticipated; would require a substantially different Infrastructure layer and is out of scope unless a distinct future business case emerges.

---

## 6. Architectural Readiness Assessment

| Future capability | Is the current architecture ready for it without rework? |
|---|---|
| FE-11 AI-assisted generation | **Yes** — consumes `ExportPackage` only; no capture-engine change needed, provided the offline-model constraint (§2.1) is respected |
| FE-06 Local network-share profiles | **Yes** — `IApplicationProfileRepository`/`ISessionRepository` (`Architecture.md` §3.2) already abstract storage location; a network-path-backed implementation is a new Infrastructure adapter, not a redesign |
| FE-09 Additional automation providers | **Yes** — `IUiAutomationProvider` (`Architecture.md` §3.2) is already the sole seam to a concrete automation library, by design |
| FE-07 CI/test-runner integration | **Partially** — export contract is ready; the generated-code-execution side (FE-07) depends on FE-11 existing first |
| FE-14 Cross-platform | **No** — would require a new Infrastructure implementation of a fundamentally different automation model (e.g., not UIA-based at all) and is not something the current design optimizes for |

---

## 7. Explicitly Rejected / Out-of-Scope Ideas

Recorded here so they are not repeatedly re-proposed without new information:

| Idea | Reason for rejection |
|---|---|
| Cloud-hosted session storage or team dashboards | Directly violates the hard offline/air-gapped constraint (`PRD.md` §9) that is foundational to the product's HSM-adjacent target environment — would require an entirely different product, not an enhancement |
| Built-in networked auto-update | Same reason — any update mechanism must remain a manual, local redistribution process (`RiskAnalysis.md` R-16) unless the offline constraint itself is formally revisited |
| Full content-aware, general-purpose redaction (detecting arbitrary sensitive data patterns in any screenshot) | Reliable general-purpose PII/secret detection in arbitrary rendered UI is a substantial ML problem on its own and was judged out of proportion to this tool's scope; FE-02's narrower, UIA-property-driven masking is the pragmatic alternative |
| Real-time collaborative recording (multiple testers annotating the same live session) | No identified demand from the stated personas (`PRD.md` §5); adds significant concurrency complexity for a single-user-per-workstation tool |

---

## 8. Enhancement Intake Process

1. A new idea (including anything in §3–§5 above) is proposed against a specific `PRD.md` Goal or a specific `RiskAnalysis.md` risk it mitigates — an enhancement with no traceable motivation is not accepted.
2. `PRD.md` is updated first (new Goal/FR, or an explicit amendment to a Constraint per §2.1's offline guardrail) before any implementation planning begins.
3. `Architecture.md`/`SystemDesign.md`/`DataModel.md` are updated as needed to reflect any structural impact, per the Architectural Readiness Assessment in §6.
4. The enhancement is added to a future `Roadmap.md` phase, following the same dependency-ordering discipline used for MVP phases.
