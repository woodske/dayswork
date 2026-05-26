# U-23 — Recurring Billing + Calendar Refresh: Domain Entities

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

This file defines the recurring-lifecycle data shapes that become authoritative once recurring day-start pricing is rebuilt from saved scope instead of from the older deposit/refund model.

See [business-logic-model.md](business-logic-model.md) for end-to-end flows and [business-rules.md](business-rules.md) for enforceable constraints.

---

## Existing types reused

| Type | Role in U-23 |
|---|---|
| `Contract` | Persistent recurring contract record containing schedule, status, saved scope, destinations, and latest known terms snapshot. |
| `ContractScopeSelection` | Durable recurring scope source of truth used for morning rebuilds. |
| `ContractTermsSnapshot` | Rebuilt pricing plus worker-energy snapshot that becomes authoritative for today's charge and runtime. |
| `PricingSnapshot` | Carries the rebuilt fixed recurring price and line-item breakdown. |
| `ContractPreview` / `ContractValidationIssue` | Existing redesign-era validity model reused when day-start rebuilds cannot produce a supported chargeable contract. |
| `ContractStatus` | Distinguishes `Active`, `Paused`, `Cancelled`, and one-time execution states that gate eligibility at day start. |
| `ContractSchedule` | Distinguishes recurring from one-time handling. |
| `GameDate` | Identifies the current in-game day and the "next eligible day" semantics for saved edits. |
| `WorkScopeSet` | U-22 runtime scope classification result consumed after U-23 authorizes a recurring shift. |
| `OutputScopeProvenance` / `OverflowCategory` | Existing runtime-output explanation shapes preserved when a recurring day actually runs. |

---

## New or locked lifecycle types

### `RecurringDayStartContext`

Pure input bundle for evaluating one active recurring contract at 6am.

```text
RecurringDayStartContext
  Contract             : Contract
  Today                : GameDate
  CurrentConfig        : ConfigSnapshot
  FestivalToday        : bool
  RainyToday           : bool
  AvailableGold        : int
```

Interpretation:
- `Contract.ScopeSelection` inside the bundled contract is the authoritative scope source
- `AvailableGold` is evaluated once at the day-start decision point
- `RainyToday` is informational for runtime/actionability, not price discounting

### `RecurringTermsRefreshOutcome`

Pure result of trying to rebuild today's recurring terms from saved scope plus current config.

```text
RecurringTermsRefreshOutcome
  Status               : RecurringTermsRefreshStatus
  RefreshedTerms       : ContractTermsSnapshot?
  ValidationIssues     : IReadOnlyList<ContractValidationIssue>
```

### `RecurringTermsRefreshStatus`

```text
RecurringTermsRefreshStatus
  { Valid, InvalidNeedsAttention, Unsupported }
```

Interpretation:
- `Valid` means a supported recurring contract produced chargeable terms
- `InvalidNeedsAttention` means the saved contract still exists but cannot currently produce valid terms
- `Unsupported` means the contract shape is outside the supported redesign-era recurring path

Both non-valid states collapse to the same player-facing needs-attention behavior in U-23.

### `RecurringChargeDecision`

Pure billing/start authorization result after a valid terms rebuild.

```text
RecurringChargeDecision
  Status               : RecurringChargeStatus
  DailyPrice           : int
  Shortfall            : int
```

### `RecurringChargeStatus`

```text
RecurringChargeStatus
  { ChargeAuthorized, CannotAfford, NoChargeFestivalSkip }
```

This keeps the post-rebuild decision explicit:
- valid and affordable -> charge/start
- valid but unaffordable -> skip with notice
- valid but festival -> skip with courtesy notice

### `RecurringDayStartOutcome`

Pure summary of the full 6am decision path for one recurring contract.

```text
RecurringDayStartOutcome
  ContractId             : ContractId
  TermsOutcome           : RecurringTermsRefreshOutcome
  ChargeDecision         : RecurringChargeDecision?
  NoticeKind             : RecurringDayStartNoticeKind?
  PersistRefreshedTerms  : bool
  StartShift             : bool
```

Interpretation:
- `PersistRefreshedTerms` is true only when the rebuild produced valid refreshed terms
- `StartShift` is true only for the charged normal recurring path
- `NoticeKind` is optional because ordinary successful recurring days stay silent

### `RecurringDayStartNoticeKind`

```text
RecurringDayStartNoticeKind
  { FestivalSkip, CannotAfford, NeedsAttention }
```

This is the deliberately narrow supported recurring notice set for U-23.

### `RecurringLifecycleChangeTiming`

Pure classification of when a player-requested recurring change should take effect.

```text
RecurringLifecycleChangeTiming
  { BeforeSixAppliesToday, AfterSixAppliesTomorrowOrLater }
```

This type is useful for keeping bulletin-board actions and lifecycle rules aligned without baking clock-cutoff logic into several UI paths independently.

---

## Existing persistent entity interpretation after U-23

### `Contract`

U-23 does not introduce a new persistent recurring root entity. It tightens the meaning of the existing `Contract` fields:

```text
Contract
  Id
  EnabledTasks
  TaskDestinations
  Schedule
  Status
  HireDate
  ScopeSelection      <- authoritative recurring rebuild source
  TermsSnapshot       <- latest known valid recurring terms snapshot
```

Key U-23 interpretation changes:
- `DepositAmount` is no longer the authoritative recurring day-start billing input
- `HourlyRate` is no longer the authoritative recurring day-start billing input
- `TermsSnapshot.Pricing.TotalPrice` becomes the authoritative recurring price of record for the latest successful rebuild

### `ContractTermsSnapshot`

U-23 tightens the operational meaning of this existing type:

```text
ContractTermsSnapshot
  Pricing
  Energy
```

For recurring contracts:
- a newly rebuilt snapshot becomes today's billing basis
- that same rebuilt snapshot becomes today's runtime energy basis
- the most recent successful rebuild replaces the saved snapshot of record

---

## Frontend- and notice-facing projections

### `RecurringContractSummaryProjection`

Projection used by bulletin-board style surfaces.

```text
RecurringContractSummaryProjection
  Status
  LatestKnownDailyPrice
  NextEligibleDayLabel
  ActionsAvailable
```

The important modeling choice is that the board shows the latest saved recurring terms snapshot, while the actual authoritative charge for a future day is still rebuilt again at that future 6am.

### `RecurringNoticePayload`

Projection for the limited same-day recurring notices.

```text
RecurringNoticePayload
  Kind
  ContractId
  DailyPrice           : int?
  Shortfall            : int?
  ValidationSummary    : string?
```

Different notice kinds use different subsets:
- `FestivalSkip` needs no shortfall
- `CannotAfford` needs price and shortfall context
- `NeedsAttention` needs contract-fix guidance based on the invalid rebuild outcome

---

## Ownership boundaries locked by this unit

| Concern | Primary owner |
|---|---|
| rebuilding recurring terms from saved scope | `ContractTermsBuilder` and pure Core pricing/validation seams |
| deciding skip vs charge vs start | recurring day-start decision seam |
| persisting the refreshed recurring terms snapshot | `ContractStore.ReplaceTermsSnapshot(...)` |
| spawning or not spawning the worker | `RecurringContractScheduler` + `ShiftOrchestrator` integration |
| sending same-day recurring notices | `MailDispatcher` |
| bulletin-board action timing and availability | frontend/bulletin-board projection layer |

This is what keeps U-23 from collapsing recurring pricing, persistence, notices, and runtime into one large orchestrator branch.
