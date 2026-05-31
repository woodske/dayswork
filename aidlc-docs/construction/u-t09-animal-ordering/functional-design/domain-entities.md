# Functional Design — Domain Entities — u-t09-animal-ordering

## Modified Entity

### `BatchKind` (enum) — `Dayswork.Core/Shifts/WorkBatch.cs`
Add a new value and narrow the meaning of an existing one.

| Value | Status | Meaning |
|---|---|---|
| `AnimalBuilding` | unchanged | Interior visit for one building (feed/pet/collect housed animals). |
| `OutdoorAnimals` | **semantics narrowed** | Now **per-building** grazing pass: pet/collect the grazing animals belonging to the building named by `LocationName`. (Was: a single farm-wide pass for all buildings' grazing animals + forage.) |
| `FarmForage` | **new** | Single farm-wide ground-forage (truffle) sweep; `CollectAnimalProducts` tile work only; no animal work. |
| `Greenhouse` | unchanged | Greenhouse crop work. |
| `OutdoorCrops` | unchanged | Outdoor crop work. |
| `OutdoorClearing` | unchanged | Outdoor clearing work. |

**Enum ordering**: insert `FarmForage` between `OutdoorAnimals` and `Greenhouse`. Values are referenced by name throughout; the numeric shift of `Greenhouse`/`OutdoorCrops`/`OutdoorClearing` is inconsequential. Note: with per-building interleaving, batch kinds are **no longer globally monotonic by enum value** — the PBT property that previously asserted Kind-ascending ordering is replaced by the structural invariants P-T09-1..3.

## Unchanged Entities (reused as-is)

### `WorkBatch` (record) — `Dayswork.Core/Shifts/WorkBatch.cs`
No shape change. For an `OutdoorAnimals` batch, `LocationName` now carries the building's selection key (home key) rather than the literal `"Farm"`; for `FarmForage`, `LocationName = "Farm"`.

### `AnimalRef` / `AnimalWorkItem` (records)
Unchanged. `AnimalRef.HomeLocation` (from `AnimalTaskHandler.ResolveHomeLocation`) already provides the per-building attribution used to scope a grazing pass.

### `WorkScopeSet`, `AnimalBuildingScope`
Unchanged. Building selection and ordering already exist.

### `AnimalTaskHandler` home keys
Unchanged. `EnumerateAnimals(farm, selectedHomeLocations)` already filters `farm.Animals` by home keys; the runtime passes a single-element set `{ building.LocationName }` for a per-building grazing pass instead of the full set.

## Relationships
- A selected `AnimalBuildingScope` maps to a contiguous pair of batches: `AnimalBuilding(LocationName)` then `OutdoorAnimals(LocationName)` (when non-feed animal tasks enabled).
- All buildings collectively map to at most one trailing `FarmForage(Farm)` batch (when `CollectAnimalProducts` enabled).

## No persistence / DTO changes
No `SaveDataSerializer`, DTO, config, or GMCM changes. `BatchKind` is a runtime-only enum (not serialized in save data).
