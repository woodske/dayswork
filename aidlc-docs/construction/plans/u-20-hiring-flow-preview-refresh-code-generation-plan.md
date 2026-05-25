# U-20 — Hiring Flow Preview Refresh: Code Generation Plan

**Unit**: U-20 — Hiring Flow Preview Refresh
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for authoritative typed draft scope, coordinator-owned preview refresh, review-first edit flow, deterministic view-model shaping, explicit invalid-preview recovery, and narrow legacy scope bootstrap.

> **This plan is the single source of truth for U-20 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-20 |
|---|---|
| **S-01** | Keeps the bulletin-board hire/edit entry points wired into the redesigned fixed-price preview flow. |
| **S-02** | Replaces the old hourly-rate preview with per-service fixed-price contribution preview on Screen 1. |
| **S-03** | Makes Screen 2 reflect typed outdoor-zone, animal-building, and greenhouse semantics instead of generic zone/building counts. |
| **S-05** | Keeps one-time vs recurring schedule selection, but changes it to drive payment-timing language rather than hourly/deposit math. |
| **S-06** | Rebuilds Screen 4 around fixed contract price, typed scope summary, worker energy summary, and invalid-preview gating. |
| **S-12** | Reworks edit flow so existing contracts reopen at review first, using authoritative saved scope/terms when present and legacy bootstrap only when needed. |
| **S-20** *(supporting)* | Ensures all new redesign-era hire/edit copy remains externalized in `i18n/default.json`. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- The hiring flow renders fixed-price preview data supplied by `ContractTermsBuilder`.
- Screen 1 shows per-service price contributions.
- Screen 2 reflects typed-scope semantics for barns/coops and greenhouse.
- Screen 4 shows fixed contract price plus worker energy summary and never mentions deposits/refunds/hours.
- Editing a recurring contract previews the revised next-day fixed price before confirmation.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Hire/edit UI flow**: `Dayswork/UI/`
  - **Core dependency surface**: `Dayswork.Core/Domain/` and `Dayswork.Core/Pricing/`
  - **Integration / composition root**: `Dayswork/Integration/ChestResolver.cs`, `Dayswork/ModEntry.cs`
  - **Tests**: `Dayswork.Tests/` with a new focused U-20 test area plus any shared generators/helpers updated in place
  - **Localization**: `Dayswork/i18n/default.json`
  - **API / Infra / Deployment artifacts**: N/A in this unit

### Explicit retrofit constraints for U-20

1. **U-20 is the player-facing switchover, not the runtime switchover.**
   Do not land worker runtime energy consumption, slower pacing, or typed-scope execution changes here. Those belong to `U-21` and `U-22`.

2. **Confirmed contracts must still populate compatibility fields for transitional consumers.**
   Even after the hire/edit flow becomes redesign-era, the confirmed `Contract` must still carry compatibility `Zones`, `DepositAmount`, and `HourlyRate` until later units migrate remaining runtime/day-start consumers off them.

3. **No whole-farm fallback may survive inside the draft flow.**
   Missing outdoor scope must remain explicit and invalid rather than being auto-filled.

4. **Output destinations remain orthogonal to pricing preview.**
   Screen 2 still manages chest/mail/bin routing, but destination changes must not alter fixed price or worker energy preview.

5. **Review-first edit flow is mandatory.**
   Editing an existing contract should open at the review screen first and only backtrack into earlier screens when the player chooses to change something.

6. **All new user-facing strings must stay externalized.**
   U-20 introduces new explanatory copy and removes obsolete hour/deposit/refund language, so i18n coverage and hardcoded-string hygiene must be preserved.

---

# PART 1 — PLANNING (this document)

Steps 1–22 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Draft model, helpers, and coordinator foundation

- [x] **Step 1 — Replace the old `ContractDraft` shape with redesign-era draft state.** Update `Dayswork/UI/ContractDraft.cs` so the draft carries authoritative typed scope, destinations, schedule, editing identity, hydration mode, and any supporting preview/view-model carriers needed by U-20. Create small additional UI-side types/files only when necessary to keep the state legible. *S-02, S-03, S-05, S-06, S-12*

- [x] **Step 2 — Add narrow compatibility helpers for edit hydration and confirm-time projection.** Introduce the smallest helper seam(s) needed to: (a) bootstrap typed scope from legacy `Zones` when authoritative scope is absent, and (b) project compatibility `Zones` back out of typed scope when building the final confirmed `Contract`. Keep these helpers explicit and local. *S-03, S-12*

- [x] **Step 3 — Refactor `Dayswork/UI/HiringFlowCoordinator.cs` to depend on the redesign preview model.** Replace the hourly/deposit preview assumptions with `IContractTermsBuilder`-driven preview refresh, coordinator-owned shared preview state, and deterministic view-model shaping. Preserve store/chest/helper ownership while removing whole-farm fallback from the draft flow itself. *S-02, S-03, S-05, S-06*

- [x] **Step 4 — Implement coordinator-owned mutation boundaries and preview refresh rules.** Land the explicit split between: task/scope mutations that rebuild preview, destination mutations that do not affect pricing, and schedule mutations that only refresh schedule-sensitive review copy. Keep all of this synchronous and testable. *S-02, S-05, S-06*

- [x] **Step 5 — Rework edit hydration and confirmation paths in `HiringFlowCoordinator`.** Editing must hydrate from `ScopeSelection` when present, fall back once to compatibility bootstrap when absent, open at review first, and confirm by persisting authoritative `ScopeSelection` + `TermsSnapshot` alongside compatibility fields for transitional consumers. *S-05, S-06, S-12*

- [x] **Step 6 — Update `Dayswork/ModEntry.cs` composition root for the new coordinator dependencies.** Construct and inject the redesign preview dependencies (`IContractTermsBuilder` and any new helper seams) without disturbing unrelated worker/runtime wiring. *Workflow / integration requirement*

## Phase B — Screen 1 fixed-price service preview

- [x] **Step 7 — Rewrite `Dayswork/UI/TaskSelectionMenu.cs` around service contribution rows.** Remove the hourly-rate preview and replace it with selected-service rows that can show charged contributions or explicit “needs scope” states while keeping Screen 1 usable before scope is complete. Preserve existing task ordering and gamepad navigation expectations. *S-01, S-02, S-06*

- [x] **Step 8 — Add coordinator/view-model mapping for Screen 1 contribution semantics.** Ensure Screen 1 service rows are derived centrally from the current preview, not recomputed in the menu, and that equivalent drafts render the same row ordering and display states. *S-02, S-19*

## Phase C — Screen 2 typed scope + output routing retrofit

- [x] **Step 9 — Rework `Dayswork/UI/ZoneAndChestMenu.cs` to reflect typed scope families.** Replace generic zone-count / whole-farm wording with separate outdoor-zone, animal-building, and greenhouse summary sections. Keep output routing on the same screen while making missing scope explicit. *S-03, S-04, S-06*

- [x] **Step 10 — Extend `ZoneDrawMenu` / building selection plumbing only as needed for supported work scope.** Update `Dayswork/UI/ZoneDrawMenu.cs`, `Dayswork/Integration/ChestResolver.cs`, or adjacent helper code so Screen 2 can select only the currently supported work-scope buildings (barns/coops and greenhouse) without regressing output-destination behavior. *S-03, S-04*

- [x] **Step 11 — Preserve and adapt output destination assignment behavior.** Keep mail/bin/chest defaulting and destination assignment working for output-producing tasks, but ensure those mutations never affect fixed price or worker-energy preview state. *S-04, S-06*

## Phase D — Schedule, summary, and review-first edit UX

- [x] **Step 12 — Refresh `Dayswork/UI/ScheduleMenu.cs` for redesign-era schedule semantics.** Keep one-time vs recurring selection intact, but remove any remaining coupling to hourly/deposit language and ensure the menu only drives schedule-sensitive review messaging. *S-05*

- [x] **Step 13 — Rewrite `Dayswork/UI/SummaryMenu.cs` around fixed price, typed scope, and worker energy.** Replace estimated-hours / rate / deposit / refund copy with fixed contract price, typed scope summary, worker energy summary, validation reasons, and schedule-sensitive payment timing. Confirm must be disabled when preview is invalid. *S-05, S-06*

- [x] **Step 14 — Update `Dayswork/UI/ContractListMenu.cs` and related callers for review-first edit entry.** Ensure Edit goes through the redesigned coordinator path and reopens the contract on Screen 4 first, with back-navigation to earlier screens only when needed. *S-01, S-12*

- [x] **Step 15 — Refresh `Dayswork/i18n/default.json` for all new U-20 copy.** Add the new redesign-era preview, scope, validation, and payment-timing strings; remove or retire U-20 screen strings that still mention hourly rates, estimated hours, deposits, or refunds; keep all text externalized. *S-20 support*

## Phase E — U-20 regression coverage

- [x] **Step 16 — Add focused U-20 example tests for coordinator and helper behavior.** Create a dedicated U-20 test area in `Dayswork.Tests/` covering draft hydration, legacy bootstrap outcomes, review-first edit entry, confirm-time contract assembly, and invalid-preview gating. Prefer pure/helper/coordinator tests over brittle menu-automation tests. *S-12, S-19*

- [x] **Step 17 — Add FsCheck coverage for U-20 orchestration invariants.** Add property tests for equivalent-draft deterministic output, no-whole-farm-fallback behavior, and schedule/destination no-price-change invariants, reusing or extending shared generators where appropriate. *S-02, S-03, S-05, S-19*

- [x] **Step 18 — Update shared test helpers, generators, and lint expectations only as needed.** Refresh any shared generator/comparer/i18n-lint support touched by the new draft/view-model surface so U-20 integrates cleanly with the existing suites without weakening coverage. *S-19, S-20 support*

## Phase F — Verification, documentation, and workflow state

- [x] **Step 19 — Build the solution with deploy disabled.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and fix any U-20 breakage before moving on. *Workflow requirement*

- [x] **Step 20 — Run the full automated test suite.** Run `dotnet test Dayswork.sln` and ensure the existing suite plus the new U-20 coverage pass cleanly. *Workflow requirement*

- [x] **Step 21 — Write the U-20 code summary artifact.** Create `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/code/code-summary.md` summarizing modified vs created files, the redesigned draft/preview flow, the compatibility bridges retained, test additions, and deliberate deferrals to U-21/U-22/U-23. *Workflow requirement*

- [x] **Step 22 — Update workflow tracking and present the standardized completion gate.** Mark completed plan steps `[x]`, update `aidlc-docs/aidlc-state.md`, append the completion audit entry, and present the required 2-option Code Generation completion message. *Workflow requirement*

---

## Risk Notes

- **Highest risk**: retrofitting the live hire/edit UI while preserving the transitional compatibility contract shape for not-yet-migrated consumers.
- **Second highest risk**: legacy edit hydration from compatibility `Zones`; the bootstrap must be honest about missing scope and avoid silently inventing state.
- **Third highest risk**: keeping output routing behavior stable while Screen 2 scope semantics and summary copy change substantially.
- **Test strategy matters here.** U-20 is player-facing and stateful, so the safety net belongs mostly at the coordinator/helper boundary rather than in UI automation.

## Artifact Output

- **Application code**:
  - `Dayswork/UI/ContractDraft.cs`
  - `Dayswork/UI/HiringFlowCoordinator.cs`
  - `Dayswork/UI/TaskSelectionMenu.cs`
  - `Dayswork/UI/ZoneAndChestMenu.cs`
  - `Dayswork/UI/ZoneDrawMenu.cs` and/or adjacent UI helper files only if needed
  - `Dayswork/UI/ScheduleMenu.cs`
  - `Dayswork/UI/SummaryMenu.cs`
  - `Dayswork/UI/ContractListMenu.cs`
  - `Dayswork/Integration/ChestResolver.cs` and nearby helper seams only if needed
  - `Dayswork/ModEntry.cs`
  - `Dayswork/i18n/default.json`
- **Tests**:
  - a new focused U-20 test area under `Dayswork.Tests/`
  - any shared generators/comparers/lint helpers updated in place
- **Documentation**:
  - `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/code/code-summary.md`
