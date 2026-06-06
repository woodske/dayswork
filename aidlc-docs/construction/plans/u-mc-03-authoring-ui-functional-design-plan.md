# Functional Design Plan - U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 - Manage Crops Authoring UI
**Stage**: CONSTRUCTION - Functional Design
**Status**: Answers received (Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A); generating artifacts

## Answer Analysis

Answers received: **Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A**. All answers are
syntactically valid single-letter choices with no "depends"/"maybe"/ambiguous wording, and
no answer contradicts another: Q1=A (single scrolling page of season rows) and Q2=B (each
season row opens a scrollable crop/fertilizer picker) compose cleanly; Q4=A (materialize
`CropZoneAssignment` on draw) and Q8=A ("Done" once ≥1 assignment exists) are mutually
consistent; Q5=A/Q6=A keep season-agnostic authoring and plan-level toggles/store-preference
out of this unit, matching the unit DoD and later units' ownership. No clarification round
required.

## Plan Checklist

- [x] Load Functional Design rule details.
- [x] Load U-MC-03 unit definition, dependency context, and story S-27.
- [x] Load requirements FR-MC-01..05, FR-MC-08 and spec §5.1, §5.2, §5.4, §6.8.
- [x] Load application-design component methods for M-24 ManageCropsMenu and M-25 CropCatalogProvider; the wired pure seams C-27 (SeasonAssignmentResolver) and C-31 (CropDescriptor).
- [x] Inspect existing `HubMenu`, `ContractDraft`, `ZoneAndChestMenu`, and the U-MC-01 crop domain types (`CropPlan`, `CropZoneAssignment`, `SeasonCropChoice`, `CropDescriptor`, `CropAssignmentMode`).
- [x] Load enabled Property-Based Testing extension rules; confirm Security Baseline disabled (N/A).
- [x] Create functional design questions using required `[Answer]:` format.
- [x] Collect complete answers for all questions.
- [x] Analyze answers for ambiguity or contradictions; raise clarifications if needed.
- [x] Generate functional design artifacts (business-logic-model.md, business-rules.md, domain-entities.md, frontend-components.md).
- [x] Present Functional Design completion gate.

## Unit Context

U-MC-03 delivers the **crop-first authoring page**: a new top-level **Manage Crops**
button on `HubMenu` opening a dedicated `ManageCropsMenu`. The player configures the
full seasonal plan (per season: crop → fertilizer → optional auto-replant), optionally
assigns an output chest for the zone(s), then begins a draw to apply the whole plan.

- **Owns**: M-24 `ManageCropsMenu`, M-25 `CropCatalogProvider`.
- **Extends**: `HubMenu` (nav row + status chip), `ContractDraft` (in-progress crop plan);
  wires C-27 (multi-season locking via `SeasonAssignmentResolver`) and C-31 (`CropDescriptor`).
- **Depends on**: U-MC-01 (crop domain + persistence; `CropPlan`/`CropZoneAssignment`/
  `SeasonCropChoice`/`CropDescriptor`/`CropAssignmentMode` already exist).
- **Stories**: S-27. **Requirements**: FR-MC-01..05, FR-MC-08.

## Grounding Facts (from current code)

- `HubMenu` builds a static list of `NavItem` rows, each with an i18n label key, an
  open-action, and a status delegate returning `Done`/`NeedsSetup`/`Optional`/`None`.
  Adding a Manage Crops row follows the existing `_items.Add(new NavItem(...))` pattern
  plus a new coordinator hook.
- `ContractDraft` already aggregates per-section selections and exposes a `ScopeSelection`
  projection. It does **not** yet carry an in-progress crop plan — U-MC-03 adds one.
- U-MC-01 domain is in place: `CropPlan { Assignments }` (no plan-level toggle/store fields
  yet), `CropZoneAssignment { Zone, Mode, Choices, OutputChest }`,
  `SeasonCropChoice { Season, Crop, StorePreference, IsLocked, OriginSeason }`,
  `CropDescriptor { …Seasons, IsMultiSeason, EffectiveDaysToFirstHarvest(...) }`,
  `CropAssignmentMode { Seasonal, SeasonAgnostic }`.
- `ZoneAndChestMenu` shows the existing "begin zone draw" handoff pattern
  (`_onBeginZoneDraw(_draft)`), and the existing zone-draw overlay (`ZoneDrawOverlay`/
  `ZoneDrawMenu`) is the machinery U-MC-04 will extend.

## In Scope

- `HubMenu` Manage Crops nav row + status chip ("Done" when ≥1 zone configured, else "Optional").
- `ManageCropsMenu` authoring page: per-season crop/fertilizer/auto-replant selection,
  season filter on the open farm, multi-season locked styling, output-chest assignment,
  begin-draw handoff; full mouse/keyboard + gamepad navigation.
- `CropCatalogProvider` (M-25): crop list from game crop data (vanilla + modded),
  season-filtered, with auto-buyable vs chest-supply-only tagging; `CropDescriptor` mapping.
- `ContractDraft` extension to carry the in-progress crop plan the menu writes.
- Wiring the pure `SeasonAssignmentResolver` (C-27) for multi-season auto-population/locking.

## Out of Scope (later units)

- Zone draw overlay coloring / overlap / red-green styling (DEV-MC-01) — **U-MC-04**.
- Applying the authored plan to drawn zones' tiles, shift crop behavior — **U-MC-05**.
- Town shopping / store-preference runtime — **U-MC-06**.
- Per-zone harvest output **routing at runtime** and greenhouse/shed runtime — **U-MC-07**.

## Functional Design Questions

Please answer each question by filling in the letter after the `[Answer]:` tag. If none
of the options match, choose the last option and describe the preference.

## Question 1 — Authoring page layout
The crop-first flow configures up to four seasons (each: crop → fertilizer → auto-replant),
then an output chest, then a draw. How should `ManageCropsMenu` lay this out?

A) **Single scrolling page** with one row per season (Spring/Summer/Fall/Winter), each row
   showing crop + fertilizer + replant controls, plus an output-chest control and a
   "Draw zone(s)" button — consistent with the existing flat single-panel menus.
B) **Hub-style sub-pages**: a small index (per-season editor, output chest, draw) that each
   open their own page, mirroring the `HubMenu` → section-page pattern.
C) A **wizard** that walks season-by-season in sequence, then output chest, then draw.
D) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 2 — Crop selection control
Within a season, the crop list can be long (vanilla + modded, season-filtered). How should
the player pick the crop and fertilizer for that season?

A) **Cycle/“<  Name  >” spinner** per season (left/right arrows step through the filtered
   crop list and the fertilizer list), matching the existing tier/recurrence cycling idiom.
B) **Scrollable list picker** opened from the season row (reuses `MenuScrollBar`), with the
   selected crop/fertilizer shown back on the season row.
C) Other (describe after `[Answer]:` tag below).

[Answer]: B

## Question 3 — Crop catalog seam & testing emphasis
M-25 `CropCatalogProvider` must read live 1.6 crop data (`Data/Crops`, seed→crop links,
season/regrow, store-stock for auto-buyable tagging). Following U-MC-02 (Q5=B: example
tests only where live Stardew APIs dominate), how should U-MC-03 structure and test this?

A) **Thin live adapter + pure mapping/filtering seam**: a `CropCatalogProvider` that reads
   live game data behind an interface, mapping to pure `CropDescriptor`s; **PBT** covers the
   pure season-filter / auto-buyable-tagging / sort logic, **example tests** cover the live
   adapter. (Determinism stays in Core.)
B) **Example tests only** for the whole provider, because live crop/shop data dominates and
   useful PBT is impractical here (mirrors U-MC-02 Q5=B exactly).
C) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 4 — Draw handoff boundary (U-MC-03 vs U-MC-04)
U-MC-04 owns the zone-draw overlay extension (red/green coloring, overlap prevention,
apply-plan-to-drawn-zones). For U-MC-03's "Draw zone(s)" button, what is the boundary?

A) **U-MC-03 wires begin-draw to the existing `ZoneDrawOverlay`/`ZoneDrawMenu` as-is** and,
   on confirm, materializes `CropZoneAssignment`(s) from the configured plan onto the drawn
   `Zone`(s) in `ContractDraft`; U-MC-04 later adds the red/green coloring, overlap rules,
   and existing-zone awareness. (U-MC-03 is end-to-end authorable now; visuals harden later.)
B) **U-MC-03 stops at authoring**: it builds and holds the configured per-season plan in the
   draft and exposes a disabled/stubbed "Draw" affordance, deferring all zone materialization
   to U-MC-04. (No `CropZoneAssignment` is produced until U-MC-04.)
C) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 5 — Season-agnostic (greenhouse/shed) authoring
FR-MC-05 says greenhouse/Grandpa's Shed present **no season option** (single continuous
assignment, `CropAssignmentMode.SeasonAgnostic`). Full greenhouse/shed runtime is U-MC-07,
but authoring is crop-first *before* a location is drawn. How should U-MC-03 handle this?

A) **Defer the SeasonAgnostic authoring path to U-MC-07**: U-MC-03 authors the open-farm
   **Seasonal** mode only (season-filtered per-season plan). The greenhouse/shed
   no-season UI and `SeasonAgnostic` plan authoring land with U-MC-07.
B) **Author both modes now**: U-MC-03 adds an explicit "Farm (seasonal)" vs
   "Greenhouse/Shed (year-round)" plan-mode toggle up front; the SeasonAgnostic mode shows a
   single unfiltered crop assignment with no season rows.
C) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 6 — Plan-level toggles & store preference scope
Spec §8.1 says `CropPlan` also holds plan-level **"clear debris before tilling"**,
**"clear dead plants"** toggles and **store config** — but the U-MC-01 `CropPlan` only has
`Assignments`, and `StorePreference` currently lives per `SeasonCropChoice`. These are
consumed by U-MC-05 (toggles) and U-MC-06 (shopping). Where should they be authored?

A) **Defer to their consuming units**: U-MC-03 authors only the per-season crop/fertilizer/
   replant plan + output chest. The global debris/dead-plant toggles are added by **U-MC-05**
   and the store preference by **U-MC-06**, each extending `CropPlan` + the Manage Crops page
   when that behavior ships. (Keeps U-MC-03 focused on the authoring core in its DoD.)
B) **Author the shells now**: U-MC-03 extends `CropPlan` with the two global toggles and a
   plan-level store preference and surfaces them on the Manage Crops page now, even though
   nothing consumes them until U-MC-05/06.
C) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 7 — Output-chest assignment UX
The player may assign an output chest shared across all seasons of the zone(s)
(`CropZoneAssignment.OutputChest`, a `ChestRef`). How should the chest be chosen here?

A) **Reuse the existing chest-selection idiom** used for task-output destinations (the
   `ChestResolver` selectable-chest list, which already excludes both built-in office chests
   per U-MC-02), presented as an optional "Output chest: <name>" picker on the page.
B) **Simple optional toggle**: "Send harvest to a chest" that opens the existing chest
   picker; when unset, leave `OutputChest` null so runtime falls back to the office output
   chest (§6.8).
C) Other (describe after `[Answer]:` tag below).

[Answer]: A

## Question 8 — Status chip & "configured" definition
FR-MC-01: the Manage Crops hub row shows **"Done"** when ≥1 zone is configured, **"Optional"**
otherwise. Given Question 4's handoff boundary, what counts as "configured" for the chip?

A) **"Done" when the draft holds ≥1 `CropZoneAssignment`** (i.e. a plan was authored *and*
   at least one zone drawn/materialized); a configured-but-undrawn plan still shows "Optional".
B) **"Done" when any per-season crop choice has been authored**, even before drawing, so the
   player sees their in-progress configuration reflected immediately.
C) Other (describe after `[Answer]:` tag below).

[Answer]: A
