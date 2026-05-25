# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Domain Entities

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

This file defines the persistence-side data shapes introduced or locked by the contract-snapshot retrofit. These shapes remain technology-agnostic and describe the business content the serializer/store must preserve. See [business-logic-model.md](business-logic-model.md) for flow and [business-rules.md](business-rules.md) for enforceable rules.

---

## Existing types reused

| Type | Role in U-19 |
|---|---|
| `Contract` | Existing bridge domain record that currently carries both historical fields (`Zones`, `DepositAmount`, `HourlyRate`) and redesign fields (`ScopeSelection`, `TermsSnapshot`). |
| `ContractScopeSelection` | Authoritative redesign scope source persisted in current-schema contracts. |
| `ContractTermsSnapshot` | Authoritative redesign pricing and energy snapshot persisted in current-schema contracts. |
| `TaskKind` | Existing service key reused in persisted enabled-task sets, destination maps, and saved pricing line items. |
| `DestinationKey` | Existing destination concept persisted unchanged through the current-schema DTO. |
| `Zone` | Reused for outdoor zone persistence and compatibility zone projection. |
| `PricingSnapshot`, `PricingLineItem`, `WorkerEnergyProfile`, `WorkActionKind` | Persisted through the saved terms snapshot introduced by U-18. |

---

## Save-envelope entities

### `DaysworkSaveDataV2`

Current authoritative save envelope.

```text
DaysworkSaveDataV2
  SchemaVersion : int
  ModVersion    : string
  Contracts     : IReadOnlyList<ContractDtoV2>
```

Business meaning:
- `SchemaVersion = 2` identifies the redesign persistence contract
- `Contracts` contain only current-schema contract entries

### `LegacyEnvelopeDisposition`

Conceptual classification used during load.

```text
LegacyEnvelopeDisposition
  { CurrentSchema,
    LegacyPreReleaseHourly,
    UnsupportedFutureSchema,
    InvalidPayload }
```

This is not necessarily a public runtime enum, but it captures the business branches the serializer follows.

---

## Current-schema contract entities

### `ContractDtoV2`

The current-schema persisted contract record.

```text
ContractDtoV2
  Id                   : string
  EnabledTasks         : IReadOnlyList<string>
  TaskDestinations     : IReadOnlyDictionary<string, DestinationDto>
  Schedule             : string
  Status               : string
  HireDate             : GameDateDto
  ScopeSelection       : ContractScopeSelectionDto
  TermsSnapshot        : ContractTermsSnapshotDto
  LegacyFinancialBridge: LegacyFinancialBridgeDto
```

Notes:
- `ScopeSelection` is authoritative for scope.
- `TermsSnapshot` is authoritative for redesign pricing/energy.
- `LegacyFinancialBridge` is compatibility-only.
- `Zones` are no longer an authoritative persisted field in current schema.

### `LegacyFinancialBridgeDto`

Temporary compatibility payload retained during the retrofit.

```text
LegacyFinancialBridgeDto
  DepositAmount : int
  HourlyRate    : int
```

Interpretation:
- persisted and rehydrated for older consumers
- not authoritative for redesigned pricing
- not used to identify legacy schema

---

## Typed scope DTO entities

### `ContractScopeSelectionDto`

Explicit persisted scope shape for redesigned contracts.

```text
ContractScopeSelectionDto
  OutdoorZones    : IReadOnlyList<ZoneDto>
  AnimalBuildings : IReadOnlyList<AnimalBuildingSelectionDto>
  Greenhouse      : GreenhouseSelectionDto?
```

This replaces the historical convention of hiding everything inside `Zones`.

### `AnimalBuildingSelectionDto`

Persisted selection of one animal building.

```text
AnimalBuildingSelectionDto
  LocationName : string
  Tier         : string
```

`Tier` aligns with U-18’s animal-building price key.

### `GreenhouseSelectionDto`

Persisted greenhouse selection.

```text
GreenhouseSelectionDto
  LocationName : string
```

### `CompatibilityZoneProjection`

Derived, non-authoritative compatibility view rebuilt from `ContractScopeSelection`.

```text
CompatibilityZoneProjection
  Zones : IReadOnlyList<Zone>
```

This is not a saved DTO. It is the load-time projection used to keep the bridge-domain `Contract.Zones` field populated for older consumers.

---

## Terms-snapshot DTO entities

### `ContractTermsSnapshotDto`

Persisted redesign terms snapshot.

```text
ContractTermsSnapshotDto
  Pricing : PricingSnapshotDto
  Energy  : WorkerEnergyProfileDto
```

### `PricingSnapshotDto`

```text
PricingSnapshotDto
  LineItems          : IReadOnlyList<PricingLineItemDto>
  OutdoorSubtotal    : int
  AnimalSubtotal     : int
  GreenhouseSubtotal : int
  TotalPrice         : int
```

### `PricingLineItemDto`

```text
PricingLineItemDto
  Family      : string
  Service     : string
  Quantity    : int
  UnitPrice   : int
  LineTotal   : int
  OutdoorBand : string?
  AnimalTier  : string?
```

### `WorkerEnergyProfileDto`

```text
WorkerEnergyProfileDto
  DailyCapacity : int
  ActionCosts   : IReadOnlyDictionary<string, int>
```

These DTOs mirror U-18’s pure terms model closely because the saved snapshot is meant to be structurally durable and testable.

---

## Contract-store mutation entities

### `TermsSnapshotReplacement`

Conceptual mutation input for the dedicated store seam.

```text
TermsSnapshotReplacement
  ContractId : ContractId
  Terms      : ContractTermsSnapshot
```

Business meaning:
- target an existing contract
- replace only its saved `TermsSnapshot`
- preserve all other contract fields

### `WholeContractUpdate`

Existing broader mutation concept retained by the store.

```text
WholeContractUpdate
  ContractId : ContractId
  Updated    : Contract
```

This remains necessary when more than the terms snapshot changes.

---

## Hydrated current-domain view

### `HydratedCurrentSchemaContract`

Conceptual view of what a successfully loaded current-schema contract contains in memory.

```text
HydratedCurrentSchemaContract
  ContractId           : ContractId
  EnabledTasks         : IReadOnlySet<TaskKind>
  CompatibilityZones   : IReadOnlyList<Zone>
  TaskDestinations     : IReadOnlyDictionary<TaskKind, DestinationKey>
  Schedule             : ContractSchedule
  Status               : ContractStatus
  HireDate             : GameDate
  DepositAmount        : int
  HourlyRate           : int
  ScopeSelection       : ContractScopeSelection
  TermsSnapshot        : ContractTermsSnapshot
```

Interpretation:
- `ScopeSelection` and `TermsSnapshot` are authoritative redesign fields
- `CompatibilityZones`, `DepositAmount`, and `HourlyRate` are bridge fields retained for callers not yet migrated

---

## Validation and load-result entities

### `CurrentSchemaContractValidity`

Conceptual classification for one v2 contract record during load.

```text
CurrentSchemaContractValidity
  { Valid,
    MissingScopeSelection,
    MissingTermsSnapshot,
    InvalidEnumValue,
    InvalidIdentifier,
    InvalidDestination,
    InvalidTermsPayload,
    InvalidScopePayload }
```

The exact implementation may use exceptions plus logging rather than an enum, but these are the business-invalid states U-19 cares about.

### `LoadResult`

Conceptual outcome for one deserialize operation.

```text
LoadResult
  LoadedContracts : IReadOnlyList<Contract>
  DroppedCount    : int
```

This captures the business idea that valid current-schema contracts can survive alongside dropped malformed ones.

---

## Derived semantic relationships

| Relationship | Meaning |
|---|---|
| `DaysworkSaveDataV2 -> ContractDtoV2` | Current-schema save envelopes contain only redesign-era contract records. |
| `ContractDtoV2 -> ContractScopeSelectionDto` | Typed scope is persisted explicitly and authoritatively. |
| `ContractDtoV2 -> ContractTermsSnapshotDto` | The saved terms snapshot is persisted explicitly and authoritatively. |
| `ContractDtoV2 -> LegacyFinancialBridgeDto` | Compatibility-only legacy financial fields remain during the retrofit. |
| `ContractScopeSelectionDto -> CompatibilityZoneProjection` | Older `Zones` consumers are served by a derived load-time projection, not by authoritative save encoding. |
| `ContractDtoV2 -> HydratedCurrentSchemaContract` | Current-schema load produces a bridge-domain `Contract` carrying both redesign and compatibility data. |
| `TermsSnapshotReplacement -> ContractStore` | Recurring repricing can update saved terms without replacing the full contract. |

---

## What these entities intentionally do not contain

- no player-facing explanation text for dropped legacy contracts
- no migration record from schema v1 to schema v2
- no runtime weather, festival, or actionable-work data
- no requirement that compatibility financial fields remain semantically equal to the redesigned terms snapshot forever

Those concerns either belong to later retrofit units or are intentionally excluded from the current persistence contract.
