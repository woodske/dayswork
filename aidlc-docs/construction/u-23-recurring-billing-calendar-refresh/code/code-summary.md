# U-23 Code Summary — Recurring Billing + Calendar Refresh

## Scope

U-23 replaces the old recurring deposit/day-start path with a rebuild-first fixed-price flow based on the saved typed scope and current config snapshot. It keeps recurring pricing stable on rain/low-work mornings, skips festivals with no charge, preserves refreshed recurring terms when rebuild succeeds, and sends the approved same-day recurring notices through the existing mail path.

## Application changes

### Core recurring day-start decision seam

- Added public recurring lifecycle domain/result types:
  - `Dayswork.Core/Domain/RecurringTermsRefreshStatus.cs`
  - `Dayswork.Core/Domain/RecurringDayStartNoticeKind.cs`
  - `Dayswork.Core/Domain/RecurringTermsRefreshOutcome.cs`
  - `Dayswork.Core/Domain/RecurringDayStartOutcome.cs`
- Added `Dayswork.Core/Pricing/RecurringDayStartDecisionEngine.cs`.
- The new engine keeps `ContractTermsBuilder` as the only authority for rebuilding recurring terms from `Contract.ScopeSelection + EnabledTasks + current config`.
- Supported outcomes are now explicit:
  - valid refresh + charge/start
  - valid refresh + festival skip
  - valid refresh + cannot-afford skip
  - invalid/unsupported rebuild + needs-attention skip

### Scheduler cutover

- Refactored `Dayswork/Orchestration/RecurringContractScheduler.cs` off the old recurring `rate / hours / deposit` path.
- The scheduler now:
  - rebuilds recurring terms first at day start
  - persists refreshed `TermsSnapshot` through `ReplaceTermsSnapshot(...)` only when the rebuild is valid
  - never charges or spawns on festival mornings
  - keeps invalid/unsupported recurring contracts active but skipped, with same-day needs-attention mail
  - keeps valid-but-unaffordable recurring contracts active but skipped, with refreshed terms preserved and same-day cannot-afford mail
  - starts recurring shifts from the refreshed snapshot that was just rebuilt
- Added per-contract exception isolation around the day-start loop.
- Adjusted one-time festival refunds to use the authoritative saved `TermsSnapshot.Pricing.TotalPrice` when available instead of the old compatibility deposit bridge.

### Mail and wording updates

- Updated `Dayswork/Integration/IMailDispatcher.cs` and `Dayswork/Integration/MailDispatcher.cs`.
- Added a dedicated `QueueNeedsAttentionNotice(...)` path.
- Changed cannot-afford mail to reference the rebuilt fixed daily price and shortfall.
- Updated recurring/festival strings in `Dayswork/i18n/default.json` to match the redesign semantics.

### Composition

- Updated `Dayswork/ModEntry.cs` to construct and inject `RecurringDayStartDecisionEngine` into the scheduler.

## Tests

Added dedicated U-23 regression/property coverage under `Dayswork.Tests/U23/`:

- `RecurringDayStartDecisionEngineTests.cs`
  - festival refresh/no-charge
  - exact affordability success
  - unsupported no-scope needs-attention outcome
  - invalid refresh preserving prior saved terms
  - valid but unaffordable refresh preserving new terms eligibility
- `U23PropertyGenerators.cs`
  - generates current-schema recurring contracts plus config/day-start inputs
- `RecurringDayStartDecisionPropertyTests.cs`
  - decision determinism
  - festival no-charge/no-spawn invariant
  - exact-price vs one-short affordability boundary
  - narrow `TermsSnapshot`-only persistence mutation on successful refresh

## Steps verified without additional code changes

- **Step 12**: pre-6am edits already flow through saved scope + saved recurring contract state, so the new rebuild-first scheduler automatically uses the latest approved edits with no one-day lag.
- **Step 13**: after-6am semantics remain future-facing. Midday cancellation is still blocked for the active shift, and pause/edit remain non-rollback state changes for future mornings.
- **Step 14**: sleep-stop behavior remains owned by the existing `CalendarHandlers -> ShiftOrchestrator.StopForSleepAndSettle()` path and was not re-coupled to billing/refund logic.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`
  - Passed with `0` errors and `0` warnings
- `dotnet test Dayswork.sln`
  - Passed with `269` tests passing and `1` expected skip
