# Functional Design — Domain Entities — U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 — Manage Crops Authoring UI
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A

This unit is an **authoring (UI) unit**. The persisted/runtime domain (`CropPlan`,
`CropZoneAssignment`, `SeasonCropChoice`, `CropDescriptor`, `CropAssignmentMode`,
`StorePreference`, `ChestRef`) already exists from **U-MC-01** and is **not redefined here**.
U-MC-03 adds the **in-progress authoring model** that the menu mutates, plus the **catalog
view model** the crop picker reads, then projects them into the existing Core types when the
player draws.

---

## 1. Existing Core entities reused (from U-MC-01) — not modified

| Entity | Role in U-MC-03 | Notes |
|---|---|---|
| `CropPlan { Assignments }` | The final authored output, built on draw-complete. | Plan-level toggles/store config **not** added here (Q6=A). |
| `CropZoneAssignment { Zone, Mode, Choices, OutputChest }` | One produced per drawn zone (Q4=A). | `Mode` is always `Seasonal` in this unit (Q5=A). |
| `SeasonCropChoice { Season, Crop, StorePreference, IsLocked, OriginSeason }` | One per configured/locked season. | `StorePreference` left at default `Either` here (authored in U-MC-06). |
| `CropDescriptor { CropItemId, SeedItemId, FertilizerItemId?, …Seasons, IsMultiSeason, EffectiveDaysToFirstHarvest() }` | The crop identity the picker selects and the resolver locks. | Produced by `CropCatalogProvider`. |
| `CropAssignmentMode { Seasonal, SeasonAgnostic }` | Always `Seasonal` for U-MC-03 output. | `SeasonAgnostic` authoring deferred to U-MC-07 (Q5=A). |
| `StorePreference { Pierre, Joja, Either }` | Carried on `SeasonCropChoice` at its default. | Not surfaced in this unit's UI. |
| `ChestRef` | The optional per-zone output chest. | Chosen via the existing chest-selection idiom (Q7=A). |
| `Zone { LocationName, TopLeft, BottomRight }` | The drawn rectangle a plan is applied to. | Produced by the reused zone-draw machinery. |

---

## 2. New authoring entities (this unit)

These live in the **`Dayswork` (mod) UI layer** alongside `ContractDraft`, except the pure
catalog mapping which lives in **`Dayswork.Core`** (Q3=A — determinism in Core).

### 2.1 `CropPlanDraft` (mod, attached to `ContractDraft`)
The mutable, in-progress crop plan the `ManageCropsMenu` edits. Mirrors the authoring shape
("configure all seasons, then draw"), separate from the immutable `CropPlan` it produces.

| Field | Type | Meaning |
|---|---|---|
| `SeasonSlots` | `Map<Season, SeasonSlotDraft>` | Up to four per-season configurations (Spring/Summer/Fall/Winter). |
| `OutputChest` | `ChestRef?` | Optional output chest shared across all seasons of the zone(s) drawn from this draft (Q7=A). |
| `MaterializedAssignments` | `List<CropZoneAssignment>` | Assignments produced by completed draws this authoring session (Q4=A). One per drawn zone. |

Derived:
- `HasAnyConfiguredSeason` — true if any `SeasonSlotDraft` has a chosen crop (drives the
  "ready to draw" affordance and partial-config feedback).
- `HasAnyAssignment` — `MaterializedAssignments.Count > 0` (drives the hub "Done" chip, Q8=A).

### 2.2 `SeasonSlotDraft` (mod)
A single season's authoring state inside `CropPlanDraft.SeasonSlots`.

| Field | Type | Meaning |
|---|---|---|
| `Season` | `Season` | The slot's season. |
| `Crop` | `CropDescriptor?` | Chosen crop, or null = unconfigured (farmhand ignores that season). |
| `Fertilizer` | `FertilizerOption?` | Chosen fertilizer, or null = none. |
| `AutoReplant` | `bool` | "Auto-replant this season's crop" toggle (default off). |
| `LockState` | `SeasonLockState` | `Open` \| `MultiSeasonLocked`. |
| `LockOrigin` | `Season?` | When `MultiSeasonLocked`, the season whose multi-season crop occupies this slot. |

A `MultiSeasonLocked` slot is **not independently editable**; its crop/fertilizer/replant are
mirrored from its `LockOrigin` slot (e.g. corn chosen in Summer locks Fall).

### 2.3 `SeasonLockState` (mod, enum)
`Open` — assignable; `MultiSeasonLocked` — occupied by a multi-season crop from another
season, non-assignable, distinctly styled, with an explanatory reason string (FR-MC-04).

### 2.4 `CropCatalogEntry` (Core view model, from `CropCatalogProvider`)
A pickable crop row: the `CropDescriptor` plus authoring-only display/tagging metadata.

| Field | Type | Meaning |
|---|---|---|
| `Crop` | `CropDescriptor` | The underlying crop identity. |
| `DisplayName` | `string` | Localized crop name for the picker. |
| `Supply` | `CropSupplyTag` | `AutoBuyable` \| `ChestSupplyOnly` (FR-MC-03). |
| `Seasons` | `IReadOnlyList<Season>` | The crop's growable seasons (for multi-season locking + filtering). |

### 2.5 `CropSupplyTag` (Core, enum)
`AutoBuyable` — seeds stocked at Pierre and/or Joja; `ChestSupplyOnly` — not stocked at any
store (ancient fruit, coffee, foraged, seed-maker output) → plant from chest stock only.

### 2.6 `FertilizerOption` (Core view model)
A pickable fertilizer row: `{ ItemId, DisplayName, Supply }` (same `CropSupplyTag` semantics).
A sentinel "None" option is represented by a null `FertilizerOption` selection.

---

## 3. Relationships

```
ContractDraft (mod)
└── CropPlanDraft  (new, 0..1)
    ├── SeasonSlots : Map<Season, SeasonSlotDraft>   (0..4 configured)
    │   └── SeasonSlotDraft
    │       ├── Crop        : CropDescriptor?         ─┐ selected from
    │       └── Fertilizer  : FertilizerOption?       ─┘ the catalog
    ├── OutputChest : ChestRef?                        (Q7=A)
    └── MaterializedAssignments : List<CropZoneAssignment>   (Q4=A; → CropPlan on Confirm)

CropCatalogProvider (Core seam, Q3=A)
└── GetCatalog(seasonFilter, greenhouseContext=false)
    ├── CropCatalogEntry  (Crop, DisplayName, Supply, Seasons)
    └── GetFertilizers() → FertilizerOption[]
```

- **Projection (draw-complete, Q4=A):** for each drawn `Zone`, the configured non-locked +
  locked `SeasonSlotDraft`s become `SeasonCropChoice`s, wrapped in a `CropZoneAssignment`
  (`Mode = Seasonal`, `OutputChest = CropPlanDraft.OutputChest`), appended to
  `MaterializedAssignments`.
- **Final build (Confirm):** `BuildContract` composes `CropPlan(MaterializedAssignments)` and
  attaches it to the `Contract` (extends the existing `BuildContract` path). Empty draft →
  `CropPlan.Empty` (opt-in; absence = no managed crops).

---

## 4. Persistence

No new persisted schema in this unit. The authored `CropPlan` already round-trips through the
U-MC-01 contract DTO (`ContractDtoV2.CropPlan`, written only when non-empty). `CropPlanDraft`
and the catalog view models are **transient authoring state** (never serialized).

---

## 5. Out of scope for this unit's entities (later units)
- `CropPlan` plan-level "clear debris"/"clear dead plants" toggles → **U-MC-05** (Q6=A).
- Plan-level / authored `StorePreference` UI → **U-MC-06** (Q6=A).
- `SeasonAgnostic` authoring model for greenhouse/shed → **U-MC-07** (Q5=A).
- Zone-draw overlay coloring/overlap entities (existing-zone color map) → **U-MC-04**.
