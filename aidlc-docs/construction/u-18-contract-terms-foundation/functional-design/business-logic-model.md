# U-18 — Contract Terms Foundation: Business Logic Model

**Unit**: U-18 — Contract Terms Foundation  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A, FD-Q10=A

Technology-agnostic flows for the new fixed-price contract-terms foundation. This unit replaces the old hourly `rate/deposit/refund/hours` seam with a pure pipeline that turns player scope selections plus enabled tasks into:
- a normalized typed work-scope set
- a fixed pricing snapshot
- a worker-energy profile
- a confirmable or invalid preview

See [domain-entities.md](domain-entities.md) for data shapes and [business-rules.md](business-rules.md) for enforceable rules.

---

## 0. Where this plugs into the redesign

Historical `U-05` priced a contract from estimated hours and deferred end-of-day settlement. U-18 removes that entire mental model.

The new pure flow is:

```text
ContractScopeSelection + EnabledTasks + ConfigSnapshot
  -> WorkScopeClassifier
  -> OutdoorServiceBandClassifier
  -> ContractPriceCalculator
  -> PriceBreakdownBuilder
  -> WorkerEnergyProfileBuilder
  -> ContractTermsBuilder
  -> ContractPreview and/or ContractTermsSnapshot
```

Important redesign differences:
- no weather input
- no festival input
- no "actual morning work found" input
- no deposit or refund calculation
- no estimated-hours explanation

The pure contract terms depend only on:
1. what the player selected
2. which services were enabled
3. the current config snapshot

---

## 1. Normalize the raw contract selection

`ContractTermsBuilder` starts from a `ContractScopeSelection`, not directly from farm state. That raw selection already contains enough pure metadata for pricing:
- outdoor rectangles
- selected animal-building identities plus their building-tier keys
- greenhouse selection

### 1.1 Preserve raw selection outside the pricing model

The raw selection is preserved for persistence and later runtime use, but the priced `WorkScopeSet` only materializes scope types relevant to the enabled tasks.

Examples:
- outdoor zones selected + only animal tasks enabled -> no outdoor priced scope
- greenhouse selected + no crop-service task enabled -> no greenhouse priced scope
- barn selected + only crop-clearing tasks enabled -> no animal priced scope

This keeps the price model focused on chargeable work rather than echoing every clicked object back as a zero-price entry.

### 1.2 Outdoor zone union

All selected outdoor rectangles are unioned into one normalized non-overlapping outdoor footprint before any service banding occurs.

That means:
- overlap never double-charges
- the normalized outdoor tile count is deterministic
- every relevant outdoor service prices from the same unioned footprint

The result is one `OutdoorWorkScope`:
- `NormalizedZones`
- `TotalTileCount`

### 1.3 Animal-building scope materialization

If at least one animal-care task is enabled, each selected barn/coop becomes its own `AnimalBuildingScope`.

Each scope carries:
- stable building identity
- building tier / capacity key

Current animal occupancy is not an input to pricing.

### 1.4 Greenhouse scope materialization

If the greenhouse is selected and at least one greenhouse-relevant crop service is enabled, one `GreenhouseWorkScope` is materialized.

Because vanilla has one greenhouse, this remains a single fixed-scope work area rather than a counted collection like barns/coops.

---

## 2. Build the typed work-scope set (`WorkScopeClassifier`)

`WorkScopeClassifier` turns the normalized, relevant portions of the raw selection into a `WorkScopeSet`.

### Inputs
- `ContractScopeSelection`
- `EnabledTasks`

### Outputs
- optional `OutdoorWorkScope`
- zero or more `AnimalBuildingScope`
- optional `GreenhouseWorkScope`

### Key modeling effect

The same task can produce chargeable scope-task pairs in multiple scope families.

Examples:
- `WaterCrops` + outdoor zones + greenhouse selected
  - one outdoor-water contribution
  - one greenhouse-water package contribution
- `HarvestCrops` + outdoor zones only
  - outdoor harvest contribution only
- `CollectAnimalProducts` + two selected deluxe barns
  - two animal-building contributions under the same service

This is why the typed `WorkScopeSet` is the foundation for both pricing and later runtime alignment.

---

## 3. Assign outdoor size bands (`OutdoorServiceBandClassifier`)

Outdoor banding happens after zone union and only for outdoor services actually enabled.

### 3.1 Shared thresholds

All outdoor services use the same threshold table, for example:
- small
- medium
- large

The exact numeric cutoffs stay configurable and are deferred to code/config design, but the threshold scheme itself is shared.

### 3.2 Service-specific classification records

Even though the thresholds are shared, the classifier still emits one `OutdoorServiceBand` per relevant outdoor service because pricing is service-specific.

For a single outdoor footprint, the output may look conceptually like:

```text
HarvestCrops -> Large
WaterCrops   -> Large
ClearRocks   -> Large
CutTrees     -> Large
```

All four services reference the same normalized outdoor tile count, but each will later read its own configurable price for `Large`.

### 3.3 No exact morning workload coupling

Band assignment does not inspect:
- ready crops
- rock count
- tree count
- weather
- pathing

This prevents recurring pricing from drifting day to day and keeps the contract price anchored to selected scope, not discovered runtime work.

---

## 4. Calculate fixed contract price (`ContractPriceCalculator`)

`ContractPriceCalculator` consumes typed scopes plus outdoor service bands and produces raw totals.

### 4.1 Outdoor pricing

For each relevant outdoor service band:
1. build an `OutdoorPriceKey(Service, Band)`
2. read the configured price for that key
3. add one outdoor contribution for that service

Because all outdoor services share thresholds but not prices, `HarvestCrops -> Large` and `ClearRocks -> Large` can price differently.

### 4.2 Animal-building pricing

Animal pricing is additive per selected service per selected building.

For each selected animal service:
1. iterate selected `AnimalBuildingScope`s
2. build an `AnimalBuildingPriceKey(Service, BuildingTier)`
3. read that configured price
4. add one contribution per matching building

Two deluxe coops with `PetAnimals` enabled produce two identical per-building contributions that will later aggregate into one breakdown line with `Quantity = 2`.

### 4.3 Greenhouse pricing

Greenhouse pricing is additive per selected greenhouse crop service.

For each greenhouse-relevant crop service selected:
1. build a `GreenhousePriceKey(Service)`
2. read the configured fixed package price
3. add one greenhouse contribution

This means `WaterCrops` and `HarvestCrops` can each add their own greenhouse package line when both are enabled.

### 4.4 Family subtotals

The calculator returns raw totals separated into:
- outdoor subtotal
- animal subtotal
- greenhouse subtotal
- total price

Those family subtotals feed later breakdown and preview rendering, but they carry no user-facing text yet.

---

## 5. Build the stable price breakdown (`PriceBreakdownBuilder`)

`PriceBreakdownBuilder` converts raw contributions into a deterministic `PricingSnapshot`.

### 5.1 Aggregate by normalized pricing key

Line items aggregate by pricing key, not by physical click instance.

Examples:
- `Harvest Crops - Outdoor Large`
- `Pet Animals - Deluxe Coop x2`
- `Water Crops - Greenhouse Package`

This keeps the persisted snapshot compact and the UI legible.

### 5.2 Line-item aggregation rules

Outdoor:
- aggregate by `(Service, Band)`
- quantity is typically `1` because pricing comes from one unioned outdoor footprint

Animal:
- aggregate by `(Service, BuildingTier)`
- quantity is the number of selected buildings of that tier

Greenhouse:
- aggregate by `(Service)`
- quantity is `1` for the single greenhouse package line

### 5.3 Deterministic ordering

The builder emits lines in a deterministic canonical order:
1. outdoor lines
2. animal lines
3. greenhouse lines

Inside each family:
- primary key: canonical service order
- secondary key: band or building-tier order where relevant

This determinism is important for:
- stable previews
- persistence diffs
- property-based equality expectations

---

## 6. Build the worker-energy profile (`WorkerEnergyProfileBuilder`)

`WorkerEnergyProfileBuilder` creates the worker's daily labor budget independently of price.

### 6.1 Daily capacity

The profile includes one daily capacity integer from config.

This is the value later shown as the worker's stamina budget and seeded into `WorkerEnergyLedger`.

### 6.2 Full action-cost table

The profile includes the full configured action-cost table for all known work actions, not only the subset obviously used by the current contract.

This decision supports:
- one-time contract snapshot stability
- clean recurring rebuilds
- future-proof runtime logic that can consume the same shape every day

### 6.3 Fine-grained work actions

Energy costs are keyed by fine-grained labor actions that correspond to real work beats, for example:
- `WaterTile`
- `HarvestCrop`
- `HarvestFruit`
- `FeedAnimal`
- `PetAnimal`
- `CollectAnimalProduct`
- `AxeSwing`
- `PickaxeSwing`
- `ScytheSwing`

This preserves the approved "farmer-like energy per action" model without reducing everything to one cost per top-level task.

---

## 7. Build preview vs. build terms (`ContractTermsBuilder`)

`ContractTermsBuilder` is the pure facade over the whole foundation pipeline.

## 7.1 `BuildPreview(...)`

Used by the live hiring flow.

### Steps
1. Normalize and classify scopes
2. Determine whether any chargeable scope-task pair exists
3. If none exists:
   - return `ContractPreview(IsValid=false)`
   - populate validation issues
   - do not attach a proposed terms snapshot
4. If at least one chargeable pair exists:
   - band outdoor services
   - calculate raw totals
   - build `PricingSnapshot`
   - build `WorkerEnergyProfile`
   - assemble `ContractTermsSnapshot`
   - return `ContractPreview(IsValid=true, ProposedTerms=...)`

### Important validity nuance

The preview becomes blocking-invalid only when the contract has zero chargeable scope-task pairs overall.

Example:
- `FeedAnimals` enabled + one coop selected + `WaterCrops` enabled but no outdoor zone
  - preview remains valid
  - animal pricing terms are produced
  - no outdoor-water contribution is produced

That keeps the contract model aligned with FD-Q8=A rather than forcing every partial mismatch to become a hard error.

## 7.2 `BuildTerms(...)`

Used for final confirmation paths that need a `ContractTermsSnapshot`.

Business expectation:
- callers should only invoke this after a valid preview
- if invoked with zero chargeable scope-task pairs, the method must fail fast rather than inventing a zero-price confirmable contract

The exact failure transport is a code-generation detail. The functional rule is simply: invalid drafts do not produce confirmable terms.

## 7.3 `RebuildTerms(...)`

Used for recurring contracts on later eligible days.

Steps:
1. read saved raw `ContractScopeSelection`
2. read saved enabled task set
3. read current `ConfigSnapshot`
4. rerun the same pure pipeline
5. replace the contract's stored terms snapshot with the rebuilt one

Because this rebuild is pure and scope/config driven, recurring price remains stable against morning crop count or weather variation unless config or saved scope actually changed.

---

## 8. One-time vs. recurring terms

### 8.1 One-time contracts

At confirmation time:
- build final `ContractTermsSnapshot`
- deduct the fixed total immediately
- persist the exact pricing snapshot and full energy profile

That snapshot is later used unchanged on the scheduled work day.

### 8.2 Recurring contracts

The raw scope and enabled tasks are the durable source of truth.

At each eligible day start:
- rebuild terms from saved scope + current config
- charge the rebuilt fixed total
- start the shift with the rebuilt energy profile

This is what allows:
- stable pricing on low-work days
- stable pricing on rainy days
- config changes to take effect the next morning

without any return to hourly settlement logic.

---

## 9. Data-flow summary

```text
Raw selection
  -> keep only scope families relevant to enabled tasks
  -> union outdoor rectangles
  -> materialize typed scopes

Typed scopes
  -> outdoor service bands (shared thresholds, service-specific records)
  -> raw price totals
  -> aggregated pricing snapshot
  -> full worker energy profile

If no chargeable scope-task pair exists
  -> invalid preview, no confirmable terms

If at least one chargeable scope-task pair exists
  -> valid preview
  -> confirmable ContractTermsSnapshot
```

---

## 10. What U-18 explicitly does not decide

- exact numeric band thresholds
- exact configured prices
- exact action-cost numbers
- exact UI copy
- exact runtime spend semantics after the profile is consumed by `WorkerEnergyLedger`
- exact persistence DTO versioning

Those stay with later Construction stages or later retrofit units.
