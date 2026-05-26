# U-22 — Scope-Driven Runtime Alignment: Domain Entities

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A (authoritative typed scope only), FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X (no older contracts to support), FD-Q9=A

This file defines the pure and bridge data shapes that U-22 relies on to turn typed scope selection into live execution and scope-aware output handling.

See [business-logic-model.md](business-logic-model.md) for runtime flows and [business-rules.md](business-rules.md) for enforceable rules.

---

## Existing types reused directly

| Type | Role in U-22 |
|---|---|
| `Contract` | Supplies enabled tasks, task-owned output destinations, and authoritative `ScopeSelection`. |
| `ContractScopeSelection` | The only supported runtime scope source. |
| `AnimalBuildingSelection` | Saved selected barn/coop reference before normalization. |
| `GreenhouseSelection` | Saved selected greenhouse reference before normalization. |
| `WorkScopeSet` | Canonical normalized runtime scope set. |
| `OutdoorWorkScope` | Normalized outdoor zone set for outdoor crop/clearing runtime planning. |
| `AnimalBuildingScope` | Normalized building-owned animal scope. |
| `GreenhouseWorkScope` | Normalized greenhouse crop-work scope. |
| `TaskKind` | Top-level task routing key that continues to own output destinations. |
| `DestinationKey` | Shipping bin / chest destination identity. |
| `DepositPlan` / `DepositTrip` | Destination-resolution and shift-end delivery plan. |

---

## Existing types whose semantics change in U-22

### `Contract`

The type remains structurally the same, but U-22 changes runtime expectations:
- `ScopeSelection` is required for supported live execution
- `Zones` is no longer a runtime planning source
- `TaskDestinations` remains authoritative per `TaskKind`

So the same data record now has clearer runtime semantics:

```text
Contract
  ScopeSelection     -> runtime scope source
  TaskDestinations   -> output routing source
  Zones              -> compatibility artifact, not runtime authority
```

### `BufferedItem`

Today `BufferedItem` carries:
- item id
- quantity
- source task

That is enough for destination routing, but not enough for scope-aware overflow mail. U-22 therefore conceptually promotes buffered output from "task-only" to "task + origin scope."

The exact implementation can evolve in code generation, but the functional data requirement is:

```text
Buffered output item
  QualifiedItemId
  Quantity
  SourceTask
  ScopeProvenance
```

### `OverflowItem`

Today `OverflowItem` carries:
- item stack
- generic `OverflowReason`

U-22 needs overflow notices to distinguish:
- cause
- originating scope family
- optional scope location

So the current two-field model is no longer expressive enough on its own.

---

## New or refined conceptual runtime types

### `ScopeFamily`

Classifies where work came from at runtime.

```text
ScopeFamily
  { Outdoor, AnimalBuilding, Greenhouse }
```

This is not about pricing; it is about execution and output provenance.

### `ScopeProvenance`

Minimal origin metadata carried with buffered output and overflow notices.

```text
ScopeProvenance
  Family         : ScopeFamily
  LocationName   : string?
```

Examples:
- outdoor weed-clearing output -> `Outdoor`, location may be the farm
- coop animal product -> `AnimalBuilding`, location `"Big Coop"`
- greenhouse fruit -> `Greenhouse`, location `"Greenhouse"`

### `ScopedBufferedItem`

Conceptual successor to the current task-only buffered item.

```text
ScopedBufferedItem
  QualifiedItemId   : string
  Quantity          : int
  SourceTask        : TaskKind
  Provenance        : ScopeProvenance
```

Responsibilities:
- `SourceTask` drives destination lookup
- `Provenance` drives overflow/unassigned mail wording

### `OverflowCause`

Separates the delivery failure reason from the scope it happened in.

```text
OverflowCause
  { NoDestinationAssigned, ChestFull, ChestMissing, NotDelivered }
```

This is conceptually the current `OverflowReason`, but named here to make the later composition clearer.

### `ScopedOverflowNotice`

Scope-aware overflow or unassigned-output record.

```text
ScopedOverflowNotice
  Stack         : ItemStack
  Cause         : OverflowCause
  SourceTask    : TaskKind
  Provenance    : ScopeProvenance
```

This becomes the authoritative input for the next-morning mail body.

### `RuntimeBatchKind`

Captures the execution batches U-22 makes explicit inside the U-21 runtime order.

```text
RuntimeBatchKind
  { AnimalBuildingWork, GreenhouseCropWork, OutdoorCropWork, OutdoorClearingWork }
```

### `RuntimeBatchDescriptor`

Describes one live runtime batch before target-level expansion.

```text
RuntimeBatchDescriptor
  Kind            : RuntimeBatchKind
  ScopeFamily     : ScopeFamily
  LocationName    : string?
  SupportedTasks  : IReadOnlySet<TaskKind>
```

Examples:
- one greenhouse batch for `WaterCrops`, `HarvestCrops`, `CollectFruit`
- one outdoor clearing batch for `ClearWeeds`, `ClearGrass`, `ClearRocks`, `CutTrees`
- one animal-building work batch spanning `FeedAnimals`, `PetAnimals`, `CollectAnimalProducts`

### `AnimalServiceTarget`

Conceptual runtime target after building-owned animal scope is resolved.

```text
AnimalServiceTarget
  HomeLocationName     : string
  CurrentLocationName  : string
  CurrentTile          : TileCoord
  AnimalId             : string
```

This makes the runtime distinction explicit:
- home building controls eligibility
- live position controls pathing

---

## Ownership boundaries locked by U-22

| Concern | Primary owner |
|---|---|
| Typed scope normalization | `WorkScopeClassifier` |
| Runtime batch shaping from normalized scope | runtime planning / `ShiftOrchestrator` seam |
| Building-owned animal eligibility | animal-target resolution seam |
| Task-owned destination lookup | `DepositPlanner` |
| Scope-aware overflow letter shaping | `MailDispatcher` + overflow-notice shaping seam |
| Scope-page wording updates | `ZoneAndChestMenu` / related UI projection seam |

The key design choice is that one concept owns routing and another owns explanation:
- `TaskKind` owns destination routing
- `ScopeProvenance` owns scope-aware mail wording

That split keeps U-22 small while still letting the player understand where undelivered items came from.
