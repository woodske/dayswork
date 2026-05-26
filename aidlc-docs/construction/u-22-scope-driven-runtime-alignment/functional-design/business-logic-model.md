# U-22 — Scope-Driven Runtime Alignment: Business Logic Model

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A (authoritative typed scope only), FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X (no older contracts to support), FD-Q9=A

Technology-agnostic runtime flows for making the live worker execute against the redesign-era typed scope model instead of relying on compatibility-era zone conventions.

This unit introduces four major runtime clarifications:
- `Contract.ScopeSelection` becomes the only supported runtime scope source
- selected barns/coops become building-owned animal scopes that work independently of outdoor zones
- greenhouse work becomes its own dedicated crop batch ahead of outdoor crop/clearing work
- overflow and unassigned-output mail becomes scope-aware while destinations remain task-owned

See [domain-entities.md](domain-entities.md) for data shapes, [business-rules.md](business-rules.md) for enforceable rules, and [frontend-components.md](frontend-components.md) for the minimal scope-page wording updates that keep the UI aligned with the runtime.

---

## 0. Where this plugs into the redesign

U-18 and U-19 established the saved typed scope model:

```text
Contract
  -> ScopeSelection
     - OutdoorZones
     - AnimalBuildings
     - Greenhouse
  -> TermsSnapshot
```

U-20 made that typed scope visible and editable in the hire flow, and U-21 moved the runtime onto energy-limited shift behavior.

U-22 is the missing live-execution bridge:

```text
Contract.ScopeSelection
  -> WorkScopeClassifier
  -> WorkScopeSet
  -> runtime batches
     - animal-building work
     - greenhouse crop work
     - outdoor crop work
     - outdoor clearing work
  -> buffer output with scope provenance
  -> DepositPlanner
  -> scope-aware overflow / unassigned-output mail
```

The important boundary is that U-22 does **not** revisit pricing, recurring billing, or energy arithmetic. It only decides how the already-saved typed scope is consumed at runtime.

---

## 1. Runtime scope intake

### 1.1 Authoritative scope source

At shift start, runtime planning reads:
- `Contract.EnabledTasks`
- `Contract.TaskDestinations`
- `Contract.ScopeSelection`

`Contract.ScopeSelection` is authoritative for U-22. `Contract.Zones` remains a compatibility artifact for earlier retrofit seams, but it no longer participates in runtime scope discovery once U-22 lands.

### 1.2 No legacy runtime fallback path

The project is not live yet, so this unit does not need a runtime execution path for older contracts that lack authoritative typed scope.

If a contract somehow reaches runtime without `ScopeSelection`, it is outside the supported U-22 contract set. The supported path is:

```text
valid redesign-era contract
  -> ScopeSelection present
  -> classify scope
  -> build batches
  -> execute shift
```

U-22 therefore simplifies the runtime rather than introducing a parallel "legacy" planner.

### 1.3 Scope classification

`WorkScopeClassifier` remains the canonical normalization seam. It converts the saved selection into:
- one normalized `OutdoorWorkScope` when outdoor services are enabled and outdoor zones exist
- zero or more `AnimalBuildingScope` entries when animal services are enabled
- one `GreenhouseWorkScope` when greenhouse-compatible services are enabled and the greenhouse is selected

This keeps runtime intake aligned with the same typed-scope language used by pricing and preview.

---

## 2. Scope-family execution flows

### 2.1 Outdoor scope flow

Outdoor zones remain the live execution boundary for:
- watering outdoor crops
- harvesting outdoor crops
- collecting outdoor fruit
- clearing weeds
- clearing grass
- clearing rocks
- cutting trees

The outdoor planner:
1. reads the normalized outdoor zones
2. scans only those zones for outdoor-compatible targets
3. builds crop and clearing batches from those targets

Unreachable or invalid tiles remain runtime-skip concerns, not scope-definition concerns.

### 2.2 Animal-building scope flow

Animal work is no longer interpreted as "whatever animals happen to be inside a zone." Instead:
1. selected barns/coops define the set of eligible home buildings
2. runtime resolves the animals assigned to those home buildings
3. runtime finds each animal at its current live location
4. the worker services those animals wherever they are, indoors or outside on the farm

This makes the selected building the job anchor and animal position a runtime execution detail.

Outdoor zones do not:
- restrict selected-building animals
- expand service to unrelated animals
- need to intersect with animal positions

### 2.3 Greenhouse scope flow

Greenhouse selection becomes a dedicated crop-work scope, not generic building geometry.

The greenhouse flow:
1. resolves the greenhouse location named by the selected greenhouse scope
2. scans only greenhouse-compatible services inside that location
3. builds a dedicated greenhouse crop batch

The greenhouse is not merged into outdoor zones, and it is not treated like an animal building.

---

## 3. Batch ordering inside the U-21 runtime shape

U-21 already established the broad runtime family order around animals first. U-22 refines the crop side of that order without changing the energy system.

The approved runtime batch order is:

```text
1. animal-building work
2. greenhouse crop work
3. outdoor crop work
4. outdoor clearing work
```

This preserves the broad "animals before the rest" behavior while letting greenhouse work act as its own premium bounded crop batch ahead of outdoor field labor.

Within each active batch family, existing nearest-next target routing may continue unless a later unit explicitly changes it.

---

## 4. Output routing under typed scope

### 4.1 Destinations remain task-owned

Output destinations stay mapped by `TaskKind`, not by scope family or building.

That means:
- outdoor `HarvestCrops` and greenhouse `HarvestCrops` share the same destination
- outdoor `CollectFruit` and greenhouse `CollectFruit` share the same destination
- `CollectAnimalProducts` remains one task-owned destination across the selected animal buildings

This keeps the live runtime aligned with the U-20 hire/edit UI, which already exposes one destination row per output-producing task.

### 4.2 Scope does not change routing resolution

Deposit resolution still uses the same planning story:
- collected output is buffered first
- `DepositPlanner` groups by destination
- chest/bin trips are planned from the resulting destination set

Typed scope changes *where output came from*, but not *how the destination key is chosen*.

---

## 5. Scope-aware overflow and unassigned-output mail

### 5.1 Why the buffer needs scope provenance

Task-owned destinations are no longer enough to explain overflow mail once one task can produce output from multiple scope families.

Examples:
- `HarvestCrops` can now mean outdoor harvest or greenhouse harvest
- `CollectFruit` can now mean outdoor tree fruit or greenhouse fruit
- `CollectAnimalProducts` can span multiple selected animal buildings

So U-22 needs each buffered output item to retain enough provenance to answer:
- which task produced this item?
- which scope family produced this item?
- if building-owned, which selected location did it belong to?

### 5.2 Mail categories become scope-aware

Overflow/unassigned-output behavior remains next-morning, no-fee, and lossless, but the notice categories are no longer fully generic.

Instead of only:
- chest full
- chest missing
- no chest assigned
- not delivered

the runtime now records those causes *with scope context*, such as:
- outdoor chest full
- greenhouse chest full
- animal-building chest full
- greenhouse had no destination assigned
- selected building chest missing

This does **not** require one letter per scope. The simplest aligned behavior is:
- one next-morning farmhand letter
- attached items consolidated as before
- body lines grouped by cause plus scope context

### 5.3 Output-routing safety invariant stays unchanged

Even with richer scope-aware reasons, the safety invariant remains:
- deposited successfully, or
- buffered and mailed next morning

No item-loss or hidden penalty logic is introduced by the richer notice categories.

---

## 6. Minimal UI wording alignment

U-22 does not redesign the scope UI again. It only introduces small wording changes so the menu still matches the live execution model.

The two key clarifications are:
- selected barns/coops mean animal care for animals assigned to those buildings, wherever they are on the farm
- selected greenhouse means a dedicated greenhouse crop work area, not a generic building selection

This keeps the UI honest without reopening the larger U-20 information-architecture work.

---

## 7. Testable properties

Property-Based Testing is enabled in partial mode. U-22 should preserve or create pure seams that support deterministic scope/runtime invariants.

| Component / seam | Category | Property to carry into code generation |
|---|---|---|
| `WorkScopeClassifier` + runtime planner | Invariant | Equivalent `ContractScopeSelection` inputs produce equivalent runtime scope families regardless of legacy `Zones`. |
| Animal-building target resolution | Invariant | Selected buildings define the eligible animal set; adding or removing outdoor zones never changes that set. |
| Greenhouse batch planning | Invariant | Greenhouse targets never appear in outdoor batches, and outdoor targets never appear in greenhouse batches. |
| Output provenance tagging | Invariant | Every buffered output item has exactly one task key and exactly one scope provenance. |
| `DepositPlanner` integration | Invariant | Destination choice depends only on `TaskKind`, while mail categorization may still depend on scope provenance. |
| Overflow notice shaping | Oracle / easy verification | Equivalent overflow inputs produce the same grouped scope-aware mail categories and preserve the one-letter-per-shift behavior. |

These are design-time property identifications per the project's partial-mode PBT rules.
