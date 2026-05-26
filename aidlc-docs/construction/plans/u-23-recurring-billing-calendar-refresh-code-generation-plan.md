# U-23 — Recurring Billing + Calendar Refresh: Code Generation Plan

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)  
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for rebuild-first recurring billing, festival/no-charge refresh behavior, invalid-rebuild needs-attention skips, silent rain/low-work mornings, same-day notice precedence, narrow `TermsSnapshot` refresh, and deterministic per-contract day-start isolation.

> **This plan is the single source of truth for U-23 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-23 |
|---|---|
| **S-05** | Replaces recurring day-start pricing with rebuilt fixed contract billing at 6am. |
| **S-12** | Aligns pre-6am and after-6am recurring edit/pause/cancel semantics with the new billing model. |
| **S-14** | Preserves predictable recurring behavior across festival, rain, and low-work mornings. |
| **S-15** | Keeps sleep-stop outcomes separate from billing and preserves no-refund recurring semantics after the day commits. |
| **S-20** | Preserves deterministic, testable recurring business logic with narrow persistence seams and strong example/property coverage. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- Recurring contracts rebuild today's `ContractTermsSnapshot` from saved scope and current config before any eligible 6am charge.
- Festival days skip with no charge while still refreshing persisted terms when rebuild succeeds.
- Rebuild-invalid recurring contracts skip with same-day needs-attention messaging instead of using stale terms.
- Rain and low-work mornings stay price-stable and silent.
- After-6am lifecycle changes remain future-facing only, with no same-day rollback or refund.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Pure Core pricing/domain seams**: `Dayswork.Core/Domain/`, `Dayswork.Core/Pricing/`, `Dayswork.Core/Persistence/`
  - **Day-start orchestration / SMAPI runtime**: `Dayswork/Orchestration/`, `Dayswork/Integration/`
  - **Player-facing wording / board flow touchpoints**: `Dayswork/UI/`, `Dayswork/i18n/default.json` only if wording changes are required by the recurring notices or timing copy
  - **Tests**: `Dayswork.Tests/` with a focused U-23 area plus recurring/persistence suites updated in place
  - **Documentation**: `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/code/`
  - **API / Infra / Deployment artifacts**: N/A in this unit

### Explicit retrofit constraints for U-23

1. **U-23 is the recurring billing/calendar cutover, not another hire-flow or runtime-scope unit.**  
   Do not reopen U-20 screen architecture or U-22 typed-scope execution behavior here.

2. **Recurring day-start authority must come from `ContractTermsBuilder`.**  
   Do not leave deposit/refund-era pricing formulas active in the scheduler.

3. **Successful recurring refresh must stay narrow.**  
   Persist refreshed recurring terms through `ReplaceTermsSnapshot(...)`, not broad whole-contract rewrites unless another field truly changes.

4. **Festival, cannot-afford, and needs-attention are the only supported same-day recurring notices.**  
   Do not add rain or low-work lifecycle mail in this unit.

5. **After-6am lifecycle changes remain future-facing.**  
   Do not introduce same-day rollback, proration, or refund behavior.

6. **Deterministic lifecycle seams are a core requirement.**  
   Rebuild result, affordability, persistence, and notice precedence must be practical to cover with example tests and FsCheck properties.

---

# PART 1 — PLANNING (this document)

Steps 1–20 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Core lifecycle decisions and persistence foundations

- [x] **Step 1 — Add the recurring day-start decision model in Core.** Introduce or extract the pure/near-pure data shapes and helper seams needed to represent rebuild outcome, charge/skip outcome, notice selection, and narrow persistence eligibility for one recurring contract morning. Target `Dayswork.Core/Domain/` plus any new helper seam under `Dayswork.Core/Pricing/` or an adjacent lifecycle-focused location. *S-05, S-12, S-14, S-20*

- [x] **Step 2 — Keep `ContractTermsBuilder` the sole recurring rebuild authority.** Refactor the relevant Core interfaces/helpers so recurring rebuilds come from `Contract.ScopeSelection` + `EnabledTasks` + current config through the existing contract-terms seams, with invalid/unsupported outcomes surfaced cleanly for the scheduler path. Target `Dayswork.Core/Pricing/ContractTermsBuilder.cs` and any supporting interfaces or helper types. *S-05, S-14, S-20*

- [x] **Step 3 — Preserve narrow successful-refresh persistence through the store seam.** Update `Dayswork.Core/Persistence/ContractStore.cs` and any contract mutation helpers so successful recurring refreshes replace only `TermsSnapshot`, while invalid rebuilds preserve the prior valid snapshot and all other contract data. *S-05, S-20*

- [x] **Step 4 — Add focused Core tests for the rebuilt recurring decision/persistence foundation.** Cover deterministic rebuild outcomes, narrow successful refresh behavior, and invalid-refresh preservation before touching the live scheduler shell. *S-05, S-14, S-20*

## Phase B — Day-start scheduler cutover and same-day notices

- [x] **Step 5 — Refactor `RecurringContractScheduler` off the deposit/refund-era recurring path.** Remove the active use of `IRateCalculator`, `IDepositCalculator`, and deposit-hours logic from recurring day start, and replace it with the rebuild-first recurring sequence from the approved design. Target `Dayswork/Orchestration/RecurringContractScheduler.cs`. *S-05, S-14, S-20*

- [x] **Step 6 — Implement the valid normal recurring day path.** Wire the scheduler so valid non-festival recurring contracts rebuild terms, check affordability against the rebuilt fixed price, deduct that price at 6am, persist refreshed terms, and start the shift from the same refreshed terms. *S-05, S-20*

- [x] **Step 7 — Implement the invalid/unsupported recurring day path.** Ensure rebuild-invalid contracts skip with no charge, no spawn, preserved prior terms, and a same-day needs-attention notice, without auto-pausing or falling back to stale terms. *S-05, S-12, S-20*

- [x] **Step 8 — Implement the festival recurring path.** Ensure festival days still rebuild recurring terms and persist them when valid, but never charge or spawn the worker, and instead send the same-day festival skip notice. *S-05, S-14*

- [x] **Step 9 — Implement the cannot-afford recurring path.** Ensure valid-but-unaffordable recurring contracts skip with no charge, keep the refreshed terms snapshot, remain active, and send the same-day cannot-afford notice. *S-05, S-12, S-14*

- [x] **Step 10 — Keep ordinary rain and low-work mornings silent.** Remove any remaining recurring pricing or notice behavior that depends on rain-adjusted billing or low-work messaging, while leaving runtime actionability itself untouched. Target `RecurringContractScheduler.cs`, `CalendarHandlers.cs`, and any related recurring helper logic. *S-14*

- [x] **Step 11 — Preserve same-day recurring notice delivery on the existing mail path.** Update `Dayswork/Integration/IMailDispatcher.cs`, `Dayswork/Integration/MailDispatcher.cs`, and any necessary i18n strings so the supported recurring notice set is explicit, same-day visible, and precedence-safe. *S-05, S-12, S-14*

## Phase C — Lifecycle timing, board semantics, and runtime alignment

- [x] **Step 12 — Align recurring edit-before-6am behavior to the rebuilt model.** Ensure the saved recurring contract flow already used by the board/hire edit path results in the next eligible 6am rebuilding from the edited scope/settings immediately, with no one-day lag. Target any relevant board/edit orchestration seam if code changes are required beyond existing U-20 persistence behavior. *S-12*

- [x] **Step 13 — Preserve after-6am future-facing pause/cancel semantics.** Verify and, if needed, tighten the bulletin-board or contract-management seams so after-6am lifecycle changes do not trigger same-day rollback, refund, or mid-shift interruption behavior. *S-12, S-15*

- [x] **Step 14 — Keep sleep-stop strictly separate from recurring billing.** Ensure U-23 does not accidentally reintroduce refund or billing logic into the already-started shift wrap-up path owned by U-21/U-22. Target `Dayswork/Orchestration/CalendarHandlers.cs`, `ShiftOrchestrator.cs`, and any recurring settlement handoff seam only if changes are necessary. *S-15*

## Phase D — Regression and property coverage

- [x] **Step 15 — Add focused U-23 example tests for recurring morning stories.** Create a dedicated U-23 test area covering at least: valid festival refresh/no-charge, exact-affordability success, invalid rebuild with needs-attention, valid but unaffordable skip with refreshed terms persisted, and future-facing after-6am cancellation semantics where the unit owns them. *S-05, S-12, S-14, S-15, S-20*

- [x] **Step 16 — Add or extend FsCheck generators for recurring day-start contexts.** Generate varied saved scope/terms/config/calendar/gold inputs that exercise valid, invalid, festival, unaffordable, rainy, and low-work lifecycle combinations without depending on the old deposit/refund model. *S-05, S-14, S-20*

- [x] **Step 17 — Add FsCheck properties for U-23 invariants.** Cover deterministic rebuild/charge outcomes, festival no-charge/no-spawn behavior, affordability boundaries, narrow terms-refresh persistence, and stable notice precedence. *S-05, S-14, S-20*

- [x] **Step 18 — Update any shared recurring/persistence helpers only as needed.** Refresh existing comparers, generators, or scheduler/persistence tests so the rebuilt recurring lifecycle integrates cleanly without weakening prior coverage. *Supporting*

## Phase E — Verification and documentation

- [x] **Step 19 — Run verification for the completed recurring lifecycle retrofit.** Execute `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln`, then fix any U-23 regressions required to restore a green build/test state. *All U-23 stories*

- [x] **Step 20 — Write the U-23 code summary and close the unit plan.** Document the modified/created files, key recurring scheduler/mail/persistence changes, and verification results in `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/code/code-summary.md`, then mark the plan complete. *All U-23 stories*
