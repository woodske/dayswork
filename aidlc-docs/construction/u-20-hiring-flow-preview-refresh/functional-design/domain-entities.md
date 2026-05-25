# U-20 — Hiring Flow Preview Refresh: Domain Entities

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A through FD-Q9=A

This file defines the player-facing draft and preview-side business shapes that U-20 introduces or locks while refreshing the hire/edit flow. These shapes stay technology-agnostic and focus on what the flow must preserve and display.

---

## Existing types reused

| Type | Role in U-20 |
|---|---|
| `ContractScopeSelection` | Authoritative draft scope source of truth during hire/edit flow |
| `ContractPreview` | Shared live preview result from `ContractTermsBuilder` |
| `ContractTermsSnapshot` | Confirmable fixed-price + worker-energy snapshot |
| `PricingSnapshot`, `PricingLineItem` | Review-screen pricing breakdown data |
| `WorkerEnergyProfile` | Review-screen worker stamina summary source |
| `TaskKind` | Task toggles and per-service contribution identity |
| `DestinationKey` | Output routing selections on Screen 2 |
| `ContractSchedule` | One-time vs recurring selection |
| `ContractId` | Existing contract identity during edit flow |
| `Zone`, `AnimalBuildingSelection`, `GreenhouseSelection` | The three families of typed scope data |

---

## Draft entities

### `ContractDraft`

The authoritative in-progress contract being edited across the four screens.

```text
ContractDraft
  EnabledTasks     : IReadOnlySet<TaskKind>
  ScopeSelection   : ContractScopeSelection
  TaskDestinations : IReadOnlyDictionary<TaskKind, DestinationKey>
  Schedule         : ContractSchedule
  EditingId        : ContractId?
  HydrationMode    : DraftHydrationMode
```

Interpretation:
- `ScopeSelection` is authoritative inside the flow.
- Legacy `Zones` are not the draft source of truth anymore.
- `HydrationMode` records whether the draft came from true typed scope or from a compatibility bootstrap.

### `DraftHydrationMode`

How an edit draft was seeded.

```text
DraftHydrationMode
  { NewDraft,
    HydratedFromAuthoritativeScope,
    DerivedFromCompatibilityZones }
```

This is mainly useful for correctness and maintenance reasoning.

---

## Preview-side entities

### `DraftPreviewState`

The current shared live preview for the active draft.

```text
DraftPreviewState
  Preview           : ContractPreview
  ServiceRows       : IReadOnlyList<ServiceContributionRow>
  ScopeSummary      : ScopeSummaryModel
  ReviewModel       : SummaryReviewModel
```

This lets the coordinator keep one preview refresh source while menus consume different slices of it.

### `ServiceContributionRow`

The Screen 1 per-service preview row.

```text
ServiceContributionRow
  Service       : TaskKind
  RowState      : ServiceContributionState
  PricingLines  : IReadOnlyList<PricingLineItem>
  DisplayAmount : int?
```

Interpretation:
- selected services remain visible even before they are chargeable
- a service may have zero, one, or multiple matching pricing lines

### `ServiceContributionState`

```text
ServiceContributionState
  { Charged,
    NeedsOutdoorScope,
    NeedsAnimalBuildingScope,
    NeedsGreenhouseScope }
```

U-20 intentionally makes these reasons explicit instead of hiding them behind invalid confirmation only.

---

## Scope-summary entities

### `ScopeSummaryModel`

The typed work-scope summary shared mainly by Screen 2 and Screen 4.

```text
ScopeSummaryModel
  OutdoorSection : OutdoorScopeSummary
  AnimalSection  : AnimalScopeSummary
  Greenhouse     : GreenhouseScopeSummary
```

### `OutdoorScopeSummary`

```text
OutdoorScopeSummary
  ZoneCount : int
  Zones     : IReadOnlyList<Zone>
```

### `AnimalScopeSummary`

```text
AnimalScopeSummary
  BuildingCount : int
  Buildings     : IReadOnlyList<AnimalBuildingSelection>
```

### `GreenhouseScopeSummary`

```text
GreenhouseScopeSummary
  Selected     : bool
  LocationName : string?
```

These summary objects make the redesign distinctions visible:
- outdoor zones
- animal buildings
- greenhouse

---

## Review-screen entities

### `SummaryReviewModel`

The Screen 4 confirm/review model.

```text
SummaryReviewModel
  SelectedTasks        : IReadOnlyList<TaskKind>
  ScopeSummary         : ScopeSummaryModel
  Pricing              : PricingSnapshot?
  WorkerEnergy         : WorkerEnergyProfile?
  PaymentTimingMessage : PaymentTimingMessage
  ValidationMessages   : IReadOnlyList<ValidationDisplayMessage>
  CanConfirm           : bool
```

Interpretation:
- `Pricing` and `WorkerEnergy` are present only when the preview is valid
- `CanConfirm` is the business gate used by Screen 4

### `PaymentTimingMessage`

Schedule-sensitive explanation shown on Screen 4.

```text
PaymentTimingMessage
  Kind : PaymentTimingKind
```

### `PaymentTimingKind`

```text
PaymentTimingKind
  { OneTimeChargeNow,
    RecurringStartsNextEligibleDay,
    RecurringEditAppliesNextEligibleDay }
```

### `ValidationDisplayMessage`

User-facing rendering of `ContractPreview.ValidationIssues`.

```text
ValidationDisplayMessage
  Code        : string
  RelatedTask : TaskKind?
```

The business meaning is that Screen 4 renders preview-invalid reasons clearly and blocks confirm.

---

## Brownfield compatibility entities

### `LegacyScopeBootstrap`

Conceptual one-time derivation used only when editing older contracts that lack authoritative scope.

```text
LegacyScopeBootstrap
  SourceZones     : IReadOnlyList<Zone>
  DerivedScope    : ContractScopeSelection
  HydrationMode   : DerivedFromCompatibilityZones
```

This is not the new long-term source of truth. It is a bridge into redesign mode for edit sessions.

### `ConfirmedDraftContract`

Conceptual result of valid confirmation.

```text
ConfirmedDraftContract
  EnabledTasks     : IReadOnlySet<TaskKind>
  ScopeSelection   : ContractScopeSelection
  TaskDestinations : IReadOnlyDictionary<TaskKind, DestinationKey>
  Schedule         : ContractSchedule
  TermsSnapshot    : ContractTermsSnapshot
  CompatibilityZones : IReadOnlyList<Zone>
```

This highlights the rule that compatibility `Zones` are derived at confirmation time, not authored by the draft.

---

## Relationships

| Relationship | Meaning |
|---|---|
| `ContractDraft -> ContractScopeSelection` | Draft scope is authoritative and typed |
| `ContractDraft -> DraftPreviewState` | The coordinator derives one current preview state from the draft |
| `DraftPreviewState -> ServiceContributionRow` | Screen 1 receives per-service contribution or “needs scope” rows |
| `DraftPreviewState -> ScopeSummaryModel` | Screen 2 and Screen 4 share typed scope summaries |
| `DraftPreviewState -> SummaryReviewModel` | Screen 4 receives one review-ready view model |
| `SummaryReviewModel -> ContractTermsSnapshot` | A valid review model exposes the terms that can be confirmed |
| `LegacyScopeBootstrap -> ContractDraft` | Older contracts can enter redesign edit flow even before all callers are modernized |
| `ConfirmedDraftContract -> Contract` | Final confirmation persists authoritative scope and terms plus derived compatibility zones |

---

## What these entities intentionally do not contain

- no hourly rate, deposit estimate, or refund estimate fields
- no hidden whole-farm fallback zone
- no player-facing wording coupled directly into Core entities
- no shift-runtime energy spending state

Those belong either to old code being retired or to later runtime units.
