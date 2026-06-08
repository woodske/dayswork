# U-MC-07 NFR Design Patterns

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Review required

All mandatory NFR Design categories were evaluated: resilience, scalability, performance, security, and logical components. No additional question round was needed because the approved Functional Design, NFR Requirements, and current code seams fix the pattern set.

The design follows the existing Manage Crops pattern: pure Core decisions, thin SMAPI adapters, fail-soft runtime barriers, and FsCheck properties for pure invariants with example/playtest coverage for live game behavior.

## P1 - Provenance-first destination precedence

Managed-crop output routing is a pure destination-selection layer:

1. Match `BufferedItem.Provenance` against managed-crop provenance destinations.
2. Fall back to existing task-level `TaskKind` assignments.
3. Fall back to `AutomaticOutputDestination`.

This is the correctness anchor for U-MC-07. It lets managed-crop harvest keep `TaskKind.HarvestCrops` for overflow categorization while preventing ordinary `HarvestCrops` destination settings from stealing per-zone managed crop output.

NFRs: REL-MC7-01, REL-MC7-02, REL-MC7-04, MAINT-MC7-01.

## P2 - Backward-compatible `DepositPlanner` overload

`DepositPlanner` gains a provenance-aware overload while the existing overload delegates with an empty provenance map. Current callers continue to resolve by task kind only.

The pure planner still groups walkable destinations and orders trips exactly as it does today; only `ResolveDestination(...)` gains the provenance-before-task lookup. This keeps ordinary deposit behavior unchanged and localizes U-MC-07 routing risk.

NFRs: PERF-MC7-02, COMPAT-MC7-05, COMPAT-MC7-06, MAINT-MC7-02.

## P3 - Stable assignment provenance keys

Managed-crop provenance uses a deterministic assignment key built from:

- `CropZoneAssignment.GroupId` when present;
- `Zone.LocationName`;
- zone top-left and bottom-right tile coordinates.

The key builder is pure and string-ordinal. Same persisted assignment identity yields the same key; distinct group/location/zone identity yields a distinct key. Legacy assignments without `GroupId` remain distinguishable through location and bounds.

NFRs: REL-MC7-05, SCALE-MC7-02, TEST-MC7-02.

## P4 - Location-scoped managed batch grouping

`ShiftPlanBuilder` emits one `BatchKind.ManagedCrops` skeleton per distinct managed assignment location, sorted deterministically. U-MC-07 extends the current farm-only filter to include:

- `Farm`;
- `Greenhouse`;
- supported expansion greenhouse locations such as `Custom_GrandpasShedGreenhouse`.

The runtime validates live location and route availability before work begins. Batch construction stays O(number of assignments) and does not inspect live maps.

NFRs: PERF-MC7-03, SCALE-MC7-01, SCALE-MC7-03, COMPAT-MC7-03.

## P5 - Live-location field reader boundary

`ManagedCropFieldReader` remains the only live-to-pure field snapshot adapter, but it becomes location-aware and caller-classified:

- read the active batch `GameLocation`;
- include only assigned zone tiles for that location;
- read live `Diggable` per candidate tile;
- set `FieldState.IsSeasonAgnosticLocation` from the caller's classification.

The pure `CropShiftPlanner` continues to decide action order, viability, and supply atomicity. The reader performs no mutation and does not scan unrelated map tiles.

NFRs: PERF-MC7-04, REL-MC7-08, MAINT-MC7-05.

## P6 - Route-gated managed-location runner

`ShiftOrchestrator.ManagedCrops` resolves the active batch location before planning work:

- `Farm` uses the current farm runtime.
- `Greenhouse` uses existing vanilla interior navigation.
- expansion greenhouse locations use `ExpansionCompatService` and the existing `ExpansionRoutePurpose.WorkEntry` / `ReturnToFarm` descriptors.

If the live location or route cannot be validated, only that managed-crop batch is skipped with diagnostics. The shift continues with remaining batches. After a shopping trip for a non-farm managed batch, the runner re-enters the active location before replanning supply-dependent work.

NFRs: REL-MC7-06, REL-MC7-07, AVAIL-MC7-02, COMPAT-MC7-04.

## P7 - Explicit season-agnostic authoring mode

The UI extends existing crop-group authoring rather than adding a new menu stack:

- `Farm` groups keep the seasonal table.
- greenhouse/shed groups show one year-round row.
- changing location clears zones.
- existing saved seasonal groups hydrate as farm seasonal by default.

This avoids a save schema bump and makes location-local coordinates visible to the player through the existing draw flow.

NFRs: COMPAT-MC7-01, COMPAT-MC7-02, USE-MC7-01, USE-MC7-02, USE-MC7-03.

## P8 - Fallback-separated chest selection

Explicit per-zone output chest selection and automatic output fallback remain separate concepts:

- explicit choices exclude built-in office input and output chests;
- null output chest remains the automatic office-output fallback;
- assigned chest failures reuse existing overflow/mail safety behavior.

This prevents the office output chest from appearing as a normal destination while preserving the familiar fallback for players who leave output unassigned.

NFRs: REL-MC7-02, REL-MC7-03, USE-MC7-04.

## P9 - Pure-property test seam plus adapter examples

Code Generation must test the pure seams with FsCheck:

- provenance key equality and distinction;
- managed-crop destination map construction;
- provenance-before-task destination precedence;
- season-agnostic draft projection and hydration;
- location-scoped batch grouping;
- greenhouse catalog filtering;
- location-scoped zone exclusion.

Live adapters remain example and manual playtest covered: menu rendering, live greenhouse/shed route validation, actual map `Diggable` reads, visible worker entry/return, shopping re-entry, and deposit pathing.

NFRs: TEST-MC7-01 through TEST-MC7-06.

## Security Pattern

Security Baseline is N/A. U-MC-07 manipulates local game state and saved contract data only. There is no network, authentication, authorization, secret, payment processor, PII, or external service surface.

NFRs: SEC-MC7-01, SEC-MC7-02, SEC-MC7-03.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Full-mode obligations are carried into P9 and the Code Generation plan; live SMAPI adapters are explicitly example/playtest-covered. |

