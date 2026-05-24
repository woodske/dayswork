# U-18 — Contract Terms Foundation: Domain Entities

**Unit**: U-18 — Contract Terms Foundation  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A, FD-Q10=A

This file defines the pure data shapes introduced or locked by the new fixed-price contract-terms model. These types live in `Dayswork.Core` and must remain free of SMAPI/Stardew runtime references. See [business-logic-model.md](business-logic-model.md) for flows and [business-rules.md](business-rules.md) for enforceable rules.

---

## Existing types reused

| Type | Role in U-18 |
|---|---|
| `TaskKind` | Existing task enum reused as the service key for pricing families. Outdoor pricing uses the outdoor-capable task members; animal pricing uses the three animal-care members; greenhouse pricing reuses the crop-service task members. |
| `Zone` | Existing tile-rectangle type reused for raw outdoor zone selection and for normalized outdoor geometry after union. |
| `Contract` | Later units persist raw scope plus the generated `ContractTermsSnapshot`, but U-18 defines the terms shape itself. |
| `ConfigSnapshot` | Supplies outdoor price tables, animal-building price tables, greenhouse package prices, daily energy capacity, and the full action-cost map. |

---

## New or locked raw-selection types

### `ContractScopeSelection`

The raw player selection that the pure pricing model consumes.

```text
ContractScopeSelection
  OutdoorZones     : IReadOnlyList<Zone>
  AnimalBuildings  : IReadOnlyList<AnimalBuildingSelection>
  Greenhouse       : GreenhouseSelection?
```

Notes:
- `OutdoorZones` are the raw rectangles the player drew on the farm.
- `AnimalBuildings` already carry the tier metadata the pure layer needs.
- `Greenhouse` is optional because the player may or may not select it.

### `AnimalBuildingSelection`

Pure representation of one selected barn/coop.

```text
AnimalBuildingSelection
  LocationName : string
  Tier         : AnimalBuildingTier
```

`LocationName` is the stable location identity later needed by persistence/runtime. `Tier` is the pricing key chosen in FD-Q4=A.

### `GreenhouseSelection`

Pure representation of the greenhouse being selected.

```text
GreenhouseSelection
  LocationName : string
```

There is only one greenhouse in vanilla play, so no multiplicity field is required.

---

## Typed priced-scope types

### `WorkScopeSet`

Normalized scope families actually relevant to the enabled task set.

```text
WorkScopeSet
  OutdoorWork      : OutdoorWorkScope?
  AnimalBuildings  : IReadOnlyList<AnimalBuildingScope>
  GreenhouseWork   : GreenhouseWorkScope?
```

Important behavior:
- irrelevant selected scope families are omitted here
- this is the priced/runtime-facing normalized view
- the raw `ContractScopeSelection` remains the persistence/source-of-truth view

### `OutdoorWorkScope`

The unioned outdoor footprint.

```text
OutdoorWorkScope
  NormalizedZones : IReadOnlyList<Zone>
  TotalTileCount  : int
```

`NormalizedZones` are non-overlapping. `TotalTileCount` is the tile count after union, not the sum of raw rectangle areas.

### `AnimalBuildingScope`

Typed priced scope for one selected animal building.

```text
AnimalBuildingScope
  LocationName : string
  Tier         : AnimalBuildingTier
```

This scope is price-stable against daily animal occupancy changes.

### `GreenhouseWorkScope`

Typed priced scope for the greenhouse.

```text
GreenhouseWorkScope
  LocationName : string
```

This is a dedicated crop-work scope, not a generic building bucket.

---

## Pricing-key types

These are structural keys, not user-facing strings. UI layers localize them later.

### `OutdoorBandSize`

```text
OutdoorBandSize { Small, Medium, Large }
```

The exact thresholds are config-driven, but the shape is the shared three-band model selected in FD-Q2=A.

### `AnimalBuildingTier`

```text
AnimalBuildingTier
  { Coop, BigCoop, DeluxeCoop, Barn, BigBarn, DeluxeBarn }
```

This is the building-tier/capacity-oriented pricing key selected in FD-Q4=A.

### `OutdoorServiceBand`

One outdoor service's band assignment over the unioned footprint.

```text
OutdoorServiceBand
  Service       : TaskKind
  Band          : OutdoorBandSize
  TotalTileCount: int
```

Even though thresholds are shared, there is still one record per relevant outdoor service.

### `OutdoorPriceKey`

```text
OutdoorPriceKey
  Service : TaskKind
  Band    : OutdoorBandSize
```

### `AnimalBuildingPriceKey`

```text
AnimalBuildingPriceKey
  Service : TaskKind
  Tier    : AnimalBuildingTier
```

### `GreenhousePriceKey`

```text
GreenhousePriceKey
  Service : TaskKind
```

The greenhouse is a fixed package per selected greenhouse crop service, so no band/tier field is needed.

---

## Pricing-result types

### `ContractPriceTotals`

Raw subtotal container returned by `ContractPriceCalculator`.

```text
ContractPriceTotals
  OutdoorSubtotal    : int
  AnimalSubtotal     : int
  GreenhouseSubtotal : int
  TotalPrice         : int
```

These are arithmetic totals only. Player-facing explanation is deferred to `PricingSnapshot`.

### `PricingFamily`

```text
PricingFamily { Outdoor, AnimalBuilding, Greenhouse }
```

### `PricingLineItem`

One stable persisted/UI breakdown line.

```text
PricingLineItem
  Family     : PricingFamily
  Service    : TaskKind
  Quantity   : int
  UnitPrice  : int
  LineTotal  : int
  OutdoorBand: OutdoorBandSize?
  AnimalTier : AnimalBuildingTier?
```

Interpretation by family:
- outdoor line -> `OutdoorBand` populated, `Quantity` typically `1`
- animal line -> `AnimalTier` populated, `Quantity` = number of selected buildings of that tier for that service
- greenhouse line -> both optional fields null, `Quantity = 1`

`PricingLineItem` is structural enough for i18n rendering later without hardcoding user-visible text into Core.

### `PricingSnapshot`

Stable persisted price explanation.

```text
PricingSnapshot
  LineItems          : IReadOnlyList<PricingLineItem>
  OutdoorSubtotal    : int
  AnimalSubtotal     : int
  GreenhouseSubtotal : int
  TotalPrice         : int
```

The line-item list must already be deterministically ordered.

---

## Energy-profile types

### `WorkActionKind`

Fine-grained labor-action key for worker stamina costs.

```text
WorkActionKind
  { WaterTile,
    HarvestCrop,
    HarvestFruit,
    FeedAnimal,
    PetAnimal,
    CollectAnimalProduct,
    AxeSwing,
    PickaxeSwing,
    ScytheSwing }
```

These keys represent labor beats, not broad task headings.

The enum may grow later, but one-time terms snapshot the full known table at build time.

### `WorkerEnergyProfile`

The worker's daily stamina contract.

```text
WorkerEnergyProfile
  DailyCapacity : int
  ActionCosts   : IReadOnlyDictionary<WorkActionKind, int>
```

This profile intentionally stores the full action-cost table, not only actions implied by the current task selection.

---

## Terms and preview types

### `ContractTermsSnapshot`

The pure snapshot later persisted with the contract and consumed by runtime.

```text
ContractTermsSnapshot
  Pricing : PricingSnapshot
  Energy  : WorkerEnergyProfile
```

This snapshot does not need to repeat the raw scope selection because later units persist that separately.

### `ContractValidationCode`

Blocking preview-failure codes for the no-chargeable-work case.

```text
ContractValidationCode
  { NoChargeableScopeTaskPair,
    NoOutdoorScopeForSelectedOutdoorService,
    NoAnimalBuildingForSelectedAnimalService,
    NoGreenhouseScopeForSelectedGreenhouseService }
```

The first code is the contract-level blocker. The others provide reason detail for UI messaging.

### `ContractValidationIssue`

Structured preview issue returned by the pure layer.

```text
ContractValidationIssue
  Code         : ContractValidationCode
  RelatedTask  : TaskKind?
```

UI layers localize `Code`; Core does not emit user-facing copy.

### `ContractPreview`

The live-preview result returned to the hiring flow.

```text
ContractPreview
  IsValid          : bool
  ValidationIssues : IReadOnlyList<ContractValidationIssue>
  ProposedTerms    : ContractTermsSnapshot?
```

Semantics:
- if `IsValid = false`, `ProposedTerms` is null
- if `IsValid = true`, `ProposedTerms` is populated and can drive preview UI directly

This lets the hiring flow show live price and energy summaries without duplicating logic outside Core.

---

## Derived semantic relationships

| Relationship | Meaning |
|---|---|
| `ContractScopeSelection -> WorkScopeSet` | Raw selection is preserved; only relevant priced scope families are materialized. |
| `OutdoorWorkScope -> OutdoorServiceBand` | One unioned outdoor scope can yield multiple service-band records. |
| `AnimalBuildingScope -> PricingLineItem` | Multiple selected buildings of the same tier can aggregate into one animal line item with `Quantity > 1`. |
| `GreenhouseWorkScope -> PricingLineItem` | One greenhouse scope can still yield multiple line items because pricing is per selected greenhouse crop service. |
| `ContractTermsSnapshot -> WorkerEnergyProfile` | One-time terms preserve the full action-cost table confirmed that day. |
| `ContractPreview -> ContractTermsSnapshot` | Valid preview carries proposed terms; invalid preview carries issues only. |

---

## What these entities intentionally do not contain

- no SMAPI/Stardew runtime objects
- no weather or festival state
- no discovered morning workload counts
- no user-visible localized strings
- no deposit/refund/hourly fields

That omission is the point of U-18: a clean pure model whose outputs stay stable under persistence, property testing, and later UI/runtime reuse.
