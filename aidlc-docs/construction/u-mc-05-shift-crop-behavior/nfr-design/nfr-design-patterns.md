# U-MC-05 NFR Design Patterns

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — NFR Design
**Status**: Review required

All mandatory NFR Design categories (resilience, scalability, performance, security,
logical components) were evaluated. No additional question round was needed: the approved
Functional Design and NFR Requirements fix the pattern set. Patterns are recommended and
consistent with the established pure-Core/thin-adapter architecture.

## P1 — Pure planner reuse, thin runtime runner (performance, determinism)
The runtime adds **no decision logic**. `CropShiftPlanner` (U-MC-01) produces the ordered
`TileAction` plan; `ManagedCropShiftRunner` only walks/animates/mutates/spends-energy and
reads live state. This keeps all testable logic in Core (NFR-MC5-01) and the hot path
allocation-light (NFR-MC5-02).

## P2 — Live→pure field-state boundary (performance, resilience)
`ManagedCropFieldReader` is the single boundary that converts live `GameLocation` state
into a pure `FieldState`. It is O(managed tiles), re-read per active zone at batch/beat
boundaries (not per tick), and never mutates. Re-reading at the zone boundary keeps the
plan fresh after harvest-first frees tiles without a farm-wide rescan.

## P3 — Action map as a total pure function (determinism)
`ManagedCropActionMap` maps each `ManagedCropActionKind` (+ live debris tool for clearing)
to its `WorkActionKind`/`WorkerTool`/tool-gated tuple. Total and deterministic → directly
PBT-covered (totality + stability). The runner consults it; it owns no branching of its own.

## P4 — Capability-skip barrier (resilience)
Before each tool-gated beat, the runner checks `CapabilityEvaluator`/`CapabilityMatrix`. A
failed check is a **non-throwing skip + one-per-reason HUD notice**; no energy spent, no
mutation, advance. This mirrors the existing outdoor skip pattern and satisfies NFR-MC5-05.
Planting/fertilizing bypass the barrier (item-gated only).

## P5 — Atomic supply ledger (item safety)
Carried supply is loaded from the input chest once at batch start and decremented per
consumed seed/fertilizer beat. Leftovers settle back to the input chest at end of shift
through an **idempotent settle** step on the existing wrap-up path. With no shopping this
unit, the planner's purchase target is computed-but-unused, guaranteeing plantings never
exceed carried stock (NFR-MC5-03).

## P6 — Coexistence via pure tile-exclusion (correctness)
`ManagedZoneTileSet.IsInManagedZone` is applied in the general crop scan so managed tiles
are partitioned out of `WaterCrops`/`HarvestCrops`. The partition is disjoint and total
(PBT property), preventing double-action (FR-MC-28).

## P7 — Batch ordering reuse (performance, predictability)
The `ManagedCrops` batch slots into the existing `ShiftPlanBuilder` ordering ahead of
general outdoor crop/clearing batches. No new scheduler; the existing batch loop, deferral,
and boundary/cap logic apply unchanged.

## P8 — Config-surfaced energy costs (consistency)
New action costs live in `WorkerEnergyProfile.ActionCosts` via `ConfigDefaults`/GMCM exactly
like existing costs — no bespoke config path, no save coupling (NFR-MC5-06).

## P9 — Forward seams (maintainability)
Two seams are deliberately left for later units without speculative code:
- Supply intake currently = input chest; U-MC-06 inserts the store trip before the
  supply-dependent beats (same `SupplyInventory`/`PurchaseLine` contract).
- Harvest routing currently = output-chest fallback; U-MC-07 inserts per-zone `ChestRef`
  resolution at the deposit step.

## Security
N/A — Security Baseline disabled for Manage Crops; no network/PII/auth/wallet surface in
this unit.

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A (disabled). |
| Property-Based Testing | Compliant, full — P1/P3/P5/P6 carry the blocking pure properties; runtime adapters example-covered. |
