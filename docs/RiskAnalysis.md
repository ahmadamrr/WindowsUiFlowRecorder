# Risk Analysis
## Windows UI Flow Recorder & Smart UI Scanner

**Document status:** Consolidates and expands every risk already referenced across `PRD.md` §11, `SystemDesign.md` §14, `Roadmap.md` (per-phase risks), `DataModel.md` §10, and `TestingStrategy.md`, and adds environment-specific risks arising from the real-world reference deployment (a Proxy App that must connect to a Hardware Security Module before an eAdmin App can be tested). This document introduces mitigations only — it does not introduce new functional requirements; any mitigation requiring a new capability must be reflected back into `PRD.md`/`Architecture.md` before implementation.

---

## 1. Purpose & Scope

Risks here fall into six categories: **Technical/UIA**, **Launch-Chain/Readiness**, **Performance/Reliability**, **Security/Compliance**, **Environment/Deployment**, and **Process/Team**. Each is rated and mitigated below. The Proxy App → eAdmin App → HSM scenario is used as the concrete worked example throughout, since it is the highest-value and highest-risk real-world use of this tool.

---

## 2. Risk Rating Methodology

| Likelihood | Meaning |
|---|---|
| Low | Unlikely to occur during normal use of the tool |
| Medium | Plausible; expected to occur occasionally across the QA team's usage |
| High | Expected to occur regularly unless mitigated |

| Impact | Meaning |
|---|---|
| Low | Minor inconvenience, workaround available, no data loss |
| Medium | Degraded capture quality, partial data loss, or a blocked session requiring manual intervention |
| High | Complete loss of session data, incorrect exported data (silently wrong, not just missing), or the tool becoming unusable in its intended (HSM-adjacent, offline) environment |

**Severity = Likelihood × Impact**, expressed as Low / Medium / High / Critical, per the standard 3×3 matrix. "Critical" is reserved for High-Likelihood × High-Impact risks only.

---

## 3. Risk Register

### 3.1 Technical / UI Automation Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-01 | Legacy Win32 controls in Proxy App or eAdmin App expose minimal/no `AutomationId`, `Name`, or `ControlType` metadata, degrading capture quality for those specific controls | Medium | Medium | Medium | `ElementInformation` (`DataModel.md` §4.7) tolerates missing `AutomationId`/`Name` (both Optional); the Scanner's search/filter (FR-6.2) still works on `ControlType`/`ClassName`/bounding position as a fallback; document known-poor-support control types in team onboarding material rather than attempting a technical fix in MVP |
| R-02 | UIA element references go stale between lookup and property read (native COM behavior), causing intermittent "element not found" during rapid interaction | High | Low | Medium | Explicitly designed-for in `SystemDesign.md` §7/§14 as an expected, recoverable condition, not an exception — re-resolved fresh on every correlation step; covered by dedicated unit/integration tests (`TestingStrategy.md` §3.2, §4) |
| R-03 | **Elevated-process UI Automation block (UIPI)**: if Proxy App or eAdmin App runs elevated (as Administrator) while the Recorder does not, Windows User Interface Privilege Isolation prevents the Recorder's UIA client and global input hook from interacting with the elevated target at all — a well-known OS-level restriction, not a bug in FlaUI or this tool | Medium–High (Proxy Apps connecting to HSM hardware are frequently run elevated) | High (recording silently fails to attach, or attaches partially with confusing gaps) | **Critical** | Document as a hard operational constraint: the Recorder must be run at the **same or higher** privilege level as every application in the launch chain. Add an explicit pre-flight check in `ApplicationLaunchOrchestrator` (`SystemDesign.md` §4) that detects "target process is elevated but Recorder is not" and surfaces this as a named, actionable error at `LaunchingChain` time (per `UC-11`) rather than a silent partial failure. Team onboarding must state this constraint up front for the Proxy/eAdmin scenario specifically |
| R-04 | Custom-drawn or non-standard controls (e.g., a bespoke status indicator for HSM connection state) may not implement the `Value`/`Text` UIA pattern needed for `ControlPropertyEquals` readiness conditions | Medium | High (blocks the entire launch-chain feature for that app) | High | `ReadinessCondition` supports `ControlPresent` (existence only) as a fallback when property comparison isn't available; recommend teams verify the specific status control's UIA pattern support using the Smart UI Scanner (`UC-15`–`UC-17`) *before* building a profile around it, not after |

### 3.2 Launch-Chain / Readiness Condition Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-05 | A `ReadinessCondition` defined against a specific control's exact text (e.g., status label must equal `"Connected"`) breaks silently if the target application's wording changes in a future release (e.g., `"HSM: Connected"` or a localized string) | Medium | Medium | Medium | `PropertyMatchMode` supports `Contains`/`Regex`, not only `Exact` (`DataModel.md` §6.4), reducing brittleness; `LaunchStepInformation.ActualWaitDurationSeconds` in the export (`DataModel.md` §4.9) gives visibility into "it took nearly the full timeout" as an early warning sign before it becomes an outright failure |
| R-06 | Readiness polling never detects success because the condition was misconfigured (wrong `AutomationId`, wrong window title pattern) — indistinguishable at first glance from "the app is genuinely not ready yet" | Medium | Medium | Medium | `UC-11`'s error message names the specific step and unmet condition explicitly (not a generic "timeout"); `SystemDesign.md` §5's "fail early after N unexpected errors" avoids waiting out a full 30s timeout when the condition is structurally broken (e.g., element never found at all vs. found-but-wrong-value) |
| R-07 | The HSM hardware/service itself is slow, unavailable, or in a degraded state on a given test machine, causing the Primary step's readiness condition to genuinely and correctly never be met — a real-world condition, not a tool defect | Medium | Low (correctly surfaced, not a data-loss risk) | Low | This is working-as-intended abort behavior (`UC-11`); mitigation is operational (verify HSM connectivity independently before a recording session) rather than a tool change; the timeout/abort design ensures the tester is never left guessing |
| R-08 | Cascading failure: if "clean up on failure" terminates the Proxy App after an eAdmin App readiness timeout, but the Proxy App's own HSM session was mid-handshake, this could leave the HSM connection in an inconsistent state on the hardware/service side | Low | Medium (outside this tool's control, but caused by this tool's action) | Medium | `CleanUpOnFailure` defaults to `true` but is explicitly configurable per step (`DataModel.md` §6.3); document that teams testing genuinely stateful hardware handshakes may wish to disable it for the Proxy App step and clean up manually, trading automatic tidiness for handshake safety |

### 3.3 Performance & Reliability Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-09 | Full-screen screenshot capture on every single action, across a long multi-hour multi-app test pass, produces very large export packages | High | Low | Medium | FR-5.1 offers window-change-only/manual-checkpoint/off modes; FR-5.3's element-cropped option further reduces size; documented as a Settings recommendation for long sessions rather than a forced default change |
| R-10 | Attaching listeners across two simultaneous processes (Proxy App + eAdmin App) plus any child dialogs increases event volume, risking missed or duplicated capture during rapid window switching | Medium | Medium | Medium | Per-context UIA subscription scoping and process-id-based correlation (`SystemDesign.md` §7); duplicate window-activation collapsing (`SystemDesign.md` §8); directly covered by `UseCases.md` UC-09 and its dedicated integration test in `TestingStrategy.md` §4 |
| R-11 | Global input hook thread stalls (e.g., due to an OS scheduling hiccup), causing Windows to silently disable the hook and stop capturing input without obvious symptoms to the tester | Low | High (silent data loss is worse than a visible failure) | Medium | Heartbeat detection with a visible overlay warning (`SystemDesign.md` §6/§14) turns a silent failure into a visible one — residual risk is limited to the (short) detection latency |
| R-12 | A crash in Proxy App or eAdmin App mid-session is handled gracefully per `UC-08`, but a crash in the *Recorder itself* (e.g., an unhandled exception in the capture pipeline) has no equivalent designed recovery | Low | High | Medium | Recommend an implementation-level global exception handler that, at minimum, attempts a best-effort flush of the in-memory session to the working session file (`SystemDesign.md` §12) before the process exits; full crash-proofing of the Recorder's own process is a candidate for `FutureEnhancements.md` if this proves insufficient in practice |
| R-13 | Real target applications (Proxy App/eAdmin App) may exhibit UIA behavior the synthetic Automation Test Harness does not reproduce, so tests pass while the real integration has a latent issue | Medium | Medium | Medium | `TestingStrategy.md` §8.5 mandates periodic real-application spot-checks specifically to catch this class of drift, rather than trusting harness-based coverage indefinitely |

### 3.4 Security & Compliance Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-14 | Screenshots and captured `Value`/`Text` fields necessarily contain whatever business/sensitive data is visibly rendered by Proxy App/eAdmin App during a test pass (e.g., real or test credentials, HSM key identifiers, account data) | High | Medium–High (depends entirely on what the target app displays) | High | Explicitly out of scope for automatic redaction in MVP (`DataModel.md` §10, `SystemDesign.md` §16); mitigation is procedural: testers control the export destination, and teams handling genuinely sensitive HSM-adjacent data should use synthetic/test data during recording passes wherever their test environment allows it — this should be stated plainly in team onboarding, not left implicit |
| R-15 | Enterprise antivirus/EDR software common in HSM-adjacent, security-hardened environments may flag or block a global low-level input hook as suspicious behavior, since that is also a technique used by real keyloggers | Medium–High (more likely precisely in the security-conscious environments this tool targets) | High (tool simply fails to capture input, possibly with a confusing security-software popup rather than a clear in-app error) | High | Document this as a known deployment consideration requiring an allow-list/exception entry in the organization's endpoint security tooling; recommend validating this during initial environment setup (before a QA team's first real recording session) rather than discovering it mid-session; no in-app mitigation is possible since this is a decision made by security software outside the tool's control |
| R-16 | Because the tool is offline-only by design (`Architecture.md` §9.3), there is no automatic update mechanism — a security or correctness fix requires manual redistribution of a new build across every QA workstation | Medium | Low–Medium | Medium | Accepted trade-off for the air-gapped constraint (`PRD.md` §9); mitigated procedurally via the team's existing internal software-distribution process, not by adding networked auto-update (which would violate the core offline requirement) |

### 3.5 Environment & Deployment Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-17 | Confusion within the team about runtime requirements: the Recorder is .NET 8, while Proxy App/eAdmin App may be .NET Framework 4.8 — a team member might incorrectly assume the Recorder needs the same runtime as the targets, or that FlaUI can't bridge the two | Medium | Low (confusion/delay, not data loss) | Low | Explicitly documented in `PRD.md` §9 (Constraints) and `SystemDesign.md` §2 (Runtime Topology): UI Automation is out-of-process/OS-level, so the Recorder's runtime is fully independent of any target application's runtime; this should be repeated in onboarding material for new team members |
| R-18 | A fully air-gapped machine may lack .NET 8 runtime pre-installed, and cannot download it on demand | Medium | Medium | Medium | Distribute the Recorder as a self-contained deployment (bundling the .NET 8 runtime) rather than a framework-dependent one, so no separate runtime installation step is needed on the target workstation — this should be confirmed as a packaging decision in `Roadmap.md` Phase 10 |
| R-19 | Screen resolution/DPI scaling differences between test workstations could cause bounding-rectangle/screenshot coordinates to be captured inconsistently across machines | Low | Low | Low | Bounding rectangles are captured in physical pixels consistently (`DataModel.md` §2); document that cross-machine visual comparison of screenshots should account for DPI differences, but automation-relevant metadata (`AutomationId`, `ControlType`, hierarchy) is unaffected by DPI |

### 3.6 Process & Team Risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| R-20 | Clean Architecture/DI discipline erodes over time as new contributors add code, e.g., a FlaUI type accidentally leaking into a ViewModel | Medium | Low–Medium | Medium | Enforced automatically, not just by review, via the architecture compliance tests in `TestingStrategy.md` §6 (dependency-direction test, no-FlaUI-leakage test) — these are merge-blocking, not advisory |
| R-21 | Knowledge concentration: if the launch-chain/readiness-condition subsystem (the most complex, highest-risk part of the codebase per `Roadmap.md` Phase 3) is understood deeply by only one contributor, its maintenance becomes a bus-factor risk | Low–Medium | Medium | Medium | Mitigated by this documentation set itself (`Architecture.md` §4/§7, `SystemDesign.md` §4–§5, `DataModel.md` §6.4, `TestingStrategy.md` §4/§9 all describe this subsystem in detail independent of any one person's memory); recommend a design-review checkpoint at the end of `Roadmap.md` Phase 3 specifically |
| R-22 | Schema churn: if `DataModel.md`'s `ExportPackage` shape changes carelessly after teams have started building downstream tooling (hand-written Page Object generators, and eventually AI-assisted generation per `FutureEnhancements.md`) against it, those consumers break | Low (once frozen per phase) | High (breaks every downstream consumer simultaneously) | Medium | `DataModel.md` §9's semver policy plus `TestingStrategy.md` §8.4's schema-version regression test together make breaking changes a deliberate, visible MAJOR-version decision rather than an accidental side effect of an unrelated change |

---

## 4. Top Risk Deep-Dives

### 4.1 R-03 — Elevated-Process UIPI Block (Critical)

**Why this ranks highest:** unlike most risks in this register, this one can cause the tool to *appear* to work (it launches the Proxy App, the process exists, a window is even visible) while silently capturing nothing, because Windows itself — not this application — is refusing the interaction. This is the single risk most likely to cause a confused, wasted debugging session for a first-time user of the Proxy App/eAdmin App profile.

**Early warning signs:** a launch chain reports success, but no `RecordedAction`s are ever appended despite visible tester interaction; or `ReadinessCondition` polling never finds an element that is visibly present on screen.

**Contingency plan:** the pre-flight elevation check (mitigation in R-03's row) should compare the Recorder process's integrity level against each target process's integrity level *before* entering `Recording`, and refuse to proceed with a specific, named error (e.g., `"eAdminApp is running elevated; restart the Recorder as Administrator to continue"`) rather than allowing a silent partial-capture session to occur at all.

### 4.2 R-15 — Endpoint Security Flagging the Global Input Hook (High)

**Why this matters specifically for this deployment:** the intended environment (HSM-adjacent, security-hardened, likely air-gapped) is, by definition, more likely to run aggressive endpoint detection than a typical corporate desktop — the very property that makes the environment appropriate for HSM testing also makes it more likely to interfere with a core mechanism (global input hooking) this tool depends on.

**Contingency plan:** this must be resolved organizationally (an allow-list entry for the Recorder executable/hook), not technically; the Roadmap should include an explicit "environment readiness check" step before a QA team's first real session, distinct from the application's own functionality.

### 4.3 R-14 — Sensitive Data Captured in Screenshots/Exports (High)

**Why this matters:** this is not a defect to be fixed but an inherent property of a tool whose entire purpose is to faithfully capture what's on screen. The risk is one of *data handling after capture*, not of the capture itself being wrong.

**Contingency plan:** procedural, not technical — team data-handling policy for exported packages (who can view them, where they may be stored/shared) should be established once, independent of this tool, and referenced from onboarding material rather than re-litigated per session.

---

## 5. Residual Risk Statement

After all mitigations in §3 are implemented, the following residual risks remain accepted as inherent to the product's design and its offline/HSM-adjacent operating context, rather than being fully eliminated:

- Some legacy/custom controls will always have imperfect UIA metadata (R-01, R-04) — the tool degrades gracefully rather than failing, but cannot manufacture metadata an application does not expose.
- Endpoint security interference (R-15) and elevation mismatches (R-03) depend on organizational/OS-level configuration outside this application's control.
- No automatic redaction of captured content exists (R-14) — this is a deliberate scope decision (`PRD.md` §4.1 Non-Goals), not an oversight.
- No networked auto-update exists (R-16) — a direct, accepted consequence of the hard offline requirement (`PRD.md` §9).

---

## 6. Risk Monitoring & Review Cadence

- This register is reviewed at the end of every `Roadmap.md` phase, since each phase is expected to surface risks more concretely than they can be assessed up front (e.g., Phase 3 is expected to sharpen R-05/R-06/R-08; Phase 10's real-application testing is expected to validate or revise R-13).
- Any risk whose Severity is reassessed as Critical during implementation triggers an immediate update to this document and, if it implies a scope change, a corresponding update to `PRD.md`/`Architecture.md` before proceeding further.
- `TestingStrategy.md` §8 (Regression Testing) and §11 (CI/Build Gate Policy) are the primary ongoing mechanisms keeping the Medium/High risks in §3 from silently regressing after initial mitigation.
