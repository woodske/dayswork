# Functional Design Plan — U-MC-01 (Crop-plan Domain + Persistence Foundation)

**Unit**: U-MC-01 — Crop-plan Domain + Persistence Foundation
**Stories**: S-34 (plan persists / migration), S-35 (technical seams + PBT)
**Owns**: C-24 ManagedCropDomain, C-25 PlantingViabilityCalculator, C-26 CropSupplyPlanner, C-27 SeasonAssignmentResolver, C-28 StoreResolver, C-29 CropShiftPlanner (skeleton), C-30 CropPlanSerialization, C-31 CropDescriptor.
**Extends**: `Contract`/`ContractScopeSelection`/`WorkScopeSet`; `SaveDataSerializer` (C-16).

## Grounding findings (from source)
- `SaveDataSerializer.CurrentSchemaVersion` is **already `3`**; the reader **rejects** any payload whose `SchemaVersion != 3` and starts fresh (no multi-version migration today). DTO classes are still named `DaysworkSaveDataV2` / `ContractDtoV2` (name lags the version integer).
- `Contract` is a record with `ScopeSelection` (`ContractScopeSelection`: OutdoorZones, AnimalBuildings, Greenhouses) + `TermsSnapshot` + tasks/destinations.
- `Zone` = `(LocationName, TopLeft, BottomRight)`; `Season` enum = Spring/Summer/Fall/Winter.
- DTOs are POCOs mapped by hand in `SaveDataSerializer`; `NullValueHandling.Ignore`.

These findings drive Question 1 (the spec's "V2→V3" predates the code already being at schema 3).

---

## Functional Design Questions

Answer each after the `[Answer]:` tag. Each notes the recommended option.

### Question 1 — Crop-plan persistence versioning & migration
Given the live envelope is already `SchemaVersion = 3` and the reader discards non-matching versions, how should the crop plan be added?

A) **Bump envelope `SchemaVersion` 3→4**, add a **nullable crop-plan DTO** to the contract DTO, rename the DTO classes to `...V3` to reflect the new shape, and **relax the reader to accept both 3 and 4** — a v3 payload migrates to an empty/disabled crop plan (explicit, spec-faithful migration; existing saves preserved, not discarded). (Recommended)
B) **Keep `SchemaVersion` at 3** and add the crop-plan as a purely **additive nullable field** on the existing contract DTO — existing v3 saves load unchanged (absent = empty plan); no reader/version change.
X) Other (please describe after [Answer]: tag below)

[Answer]: B

### Question 2 — Domain modeling of per-zone season assignments
How should `CropZoneAssignment` model up-to-four season choices, multi-season locking, and the season-agnostic greenhouse/shed case?

A) **`CropZoneAssignment`** holds the reused `Zone` + a map of `Season → SeasonCropChoice` (≤4 on the farm) + per-season auto-replant flags + optional output `ChestRef`. `SeasonCropChoice` carries `{ SeedItemId, FertilizerItemId?, IsMultiSeasonLocked, OriginSeason? }`. Greenhouse/shed zones use a **single season-agnostic choice** (a dedicated marker) instead of four seasons. (Recommended)
B) Separate domain types for farm zones (four seasons) vs greenhouse/shed zones (single continuous crop).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3 — CropPlan vs ManagedCropWorkScope relationship
Where does the authored plan live, and what is the runtime scope?

A) **`CropPlan` lives on `Contract`** (authored config: zones + per-season choices + global toggles + store preference); **`ManagedCropWorkScope`** is the runtime projection carried in `WorkScopeSet`, derived from the plan (the locations/zones to service) — mirroring how `OutdoorWorkScope`/`GreenhouseWorkScope` relate to `ContractScopeSelection`. (Recommended)
B) Put the entire `CropPlan` directly in `WorkScopeSet`; no separate `Contract` field.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Depth of pure planning logic in this foundation unit
How much of C-25..C-29 should U-MC-01 implement now?

A) **Implement the fully-functional pure planners now** (viability, supply/`min`, season locking, store/fallback; C-29 shift planner as a thin composition) with complete FsCheck coverage — the foundation owns all pure logic (App-Design Q1=A); runtime units U-MC-05/06 only wire them to the live world. (Recommended)
B) U-MC-01 implements only domain + serialization; the pure planners are stubs completed in U-MC-05/06.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 5 — Item identity representation
How are seeds/fertilizer/crops referenced in Core?

A) **Opaque 1.6 qualified item-ID strings** (e.g. `(O)472`) in Core; validated/resolved at the Mod boundary (catalog M-25). No typed wrapper in this unit. (Recommended)
B) Introduce a typed `ItemId` value object in Core now.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 6 — PBT obligations to implement in U-MC-01
PBT is full-mode. Confirm the properties implemented now.

A) **Implement now**: crop-plan DTO round-trip `deserialize(serialize(x))==x` (PBT-02); viability determinism + greenhouse bypass; `min(seeds,fertilizer)` completion + never-one-without-both; multi-season auto-populate/lock invariants; store/fallback determinism (PBT-03); seed+shrink logging on failure (PBT-08). (Recommended)
B) Implement only the DTO round-trip now; defer the planner properties to U-MC-05/06.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Plan checklist (Functional Design generation, after answers approved)
- [x] `business-logic-model.md` — crop-plan domain, pure planner responsibilities, persistence/migration flow.
- [x] `business-rules.md` — BR-MC1-* rules + PBT property table.
- [x] `domain-entities.md` — `CropPlan`/`CropZoneAssignment`/`SeasonCropChoice`/`StorePreference`/`ManagedCropWorkScope`/`CropDescriptor` + DTOs.

When done, tell me (e.g. "done"); I'll check for ambiguity and generate the FD artifacts.
