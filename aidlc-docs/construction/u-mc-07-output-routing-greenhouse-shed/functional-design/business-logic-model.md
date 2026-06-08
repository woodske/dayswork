# Business Logic Model - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

## Scope

U-MC-07 completes the Manage Crops construction loop by adding the last two behaviors deferred from U-MC-05 and U-MC-06:

- Per-zone managed-crop harvest output routing: each zone's harvested crops route to the zone's assigned `ChestRef`; zones without an assigned chest keep the current automatic office-output fallback.
- Season-agnostic managed-crop support for the vanilla `Greenhouse` and SVE `Custom_GrandpasShedGreenhouse`, including no-season authoring, live-map `Diggable` checks, existing expansion routes, and manual playtest validation.

Out of scope: new shop rules, new SVE routes, new pricing, new save schema, and any change to ordinary non-managed `HarvestCrops` destination behavior.

## Flow 1: Author a Managed-Crop Location

1. Each crop group gains an explicit managed-crop location context:
   - `Farm` = seasonal mode, current U-MC-03 four-season editor.
   - `Greenhouse` = season-agnostic mode.
   - SVE `Custom_GrandpasShedGreenhouse` = season-agnostic mode when the expansion profile exposes a valid route.
2. In season-agnostic mode, the editor shows one continuous crop row instead of Spring/Summer/Fall/Winter rows.
3. The crop picker calls `CropCatalogProvider.GetCatalog(seasonFilter: null, greenhouse: true)`, so the crop list is not season-filtered.
4. The group still supports fertilizer, replant, output chest, and drawn zones.
5. The draw session opens on the selected live location and persists zones with that location name.
6. Existing groups remain `Farm` seasonal by default to preserve saves and edit-flow hydration.

## Flow 2: Build Managed-Crop Batches

1. `ShiftPlanBuilder` reads `WorkScopeSet.ManagedCrops.Assignments`.
2. It emits one `BatchKind.ManagedCrops` per distinct managed-crop location.
3. Managed-crop batches run before general crop work for the same location:
   - managed greenhouse/shed crop batches before ordinary greenhouse crop batches;
   - managed farm crop batches before ordinary outdoor crop/clearing batches.
4. Empty crop plans emit no managed-crop batch.
5. Locations without a valid live location or valid expansion route are skipped with diagnostics rather than aborting the shift.

## Flow 3: Enter and Work Season-Agnostic Locations

1. For `Farm`, the current U-MC-05 runtime remains the baseline.
2. For vanilla `Greenhouse`, the worker enters using the existing building/interior navigation path.
3. For SVE `Custom_GrandpasShedGreenhouse`, the worker enters using the existing `ExpansionCompatService` route for `ExpansionRoutePurpose.WorkEntry`.
4. The managed-crop runner uses the current batch's live `GameLocation` rather than assuming `Game1.getFarm()`.
5. `ManagedCropFieldReader` sets `FieldState.IsSeasonAgnosticLocation = true` when the live location is greenhouse-like:
   - vanilla `Greenhouse`;
   - locations whose live Stardew data reports greenhouse behavior;
   - expansion descriptors with `ExpansionLocationRole.GreenhouseWork`.
6. `CropShiftPlanner` resolves the first `SeasonCropChoice` for `CropAssignmentMode.SeasonAgnostic`; its `Season` value is a carrier only and does not gate execution.
7. The viability gate is bypassed for season-agnostic field states, while seed/fertilizer atomicity still applies.
8. Every till/plant candidate uses the live map's `Diggable` Back-layer property, including alternate or cleared map variants.

## Flow 4: Preserve Shopping and Supply Behavior

1. Input chest supply remains the first availability gate for all managed-crop locations.
2. If a season-agnostic batch needs shopping, U-MC-06's shopping phase still applies: buy from live Pierre/Joja stock, deduct wallet gold only for granted items, and deposit bought supplies into the input chest.
3. After a shopping return/deposit for a non-farm managed-crop batch, the worker re-enters the active managed-crop location before replanning supply-dependent crop work.
4. Shopping continues to cost time only, not energy.
5. If a route to or from the greenhouse/shed is unavailable, the batch skips shopping or crop work safely and continues the rest of the shift.

## Flow 5: Tag Managed-Crop Harvest Output

1. When a managed-crop harvest action is planned, the runtime resolves the owning `CropZoneAssignment`.
2. The action carries a stable managed-crop provenance key derived from assignment identity:
   - prefer persisted `GroupId`;
   - include location and zone bounds so two groups or legacy assignments do not collide.
3. `InvokeHarvest` still buffers harvested items as `TaskKind.HarvestCrops`, but its `OutputScopeProvenance` becomes managed-crop-specific for that assignment.
4. Ordinary general `HarvestCrops` work keeps its current outdoor/greenhouse provenance and task-level destination behavior.

## Flow 6: Resolve Per-Zone Output Destinations

1. At deposit planning time, a pure managed-crop output map is built:
   - managed-crop provenance key -> `ChestDestination(assignment.OutputChest)` when assigned;
   - no entry or `AutomaticOutputDestination` when the assignment has no output chest.
2. `DepositPlanner` resolves destinations in this order:
   - managed-crop provenance destination, when present;
   - existing task-level destination map, for ordinary tasks;
   - automatic output fallback.
3. Assigned chests produce normal walkable deposit trips, including farm, interior, and expansion deposit locations already supported by the deposit runtime.
4. Missing, busy, full, or unreachable assigned chests use the existing overflow/mail safety behavior.
5. Null per-zone output chest keeps today's fallback: the worker holds the harvest and the automatic output dispatcher sends it to the farmhand office output chest or overflow path at shift completion.

## Flow 7: Exit and Deposit from Expansion Locations

1. If a managed-crop batch ends while the worker is inside vanilla greenhouse, the existing interior-exit path returns the worker to the farm before deposit/exit.
2. If a batch ends inside SVE Grandpa's Shed greenhouse, the existing expansion return route brings the worker back to the farm.
3. Deposit trips to `Custom_GrandpasShed` and `Custom_GrandpasShedGreenhouse` reuse the expansion deposit-route support already present in `ShiftOrchestrator.Deposit`.
4. Failure to validate or walk an expansion deposit route marks that trip undelivered and preserves items through overflow/mail.

## Testable Properties

| Component | Property category | Property |
|---|---|---|
| Managed-crop output destination resolver | Invariant | A buffered item with managed-crop provenance and an assigned chest resolves to that chest regardless of task-level `HarvestCrops` destinations. |
| Managed-crop output destination resolver | Invariant | A managed-crop item without an assigned chest resolves to automatic output fallback. |
| Managed-crop output destination resolver | Invariant | Ordinary non-managed `HarvestCrops` items continue to resolve from the task-level destination map. |
| Managed-crop provenance key builder | Invariant | Equal assignment identity yields equal provenance; distinct location/zone/group identities yield distinct provenance. |
| Season-agnostic field-state classifier | Invariant | Greenhouse and expansion greenhouse locations set `IsSeasonAgnosticLocation = true`; ordinary farm locations set it false. |
| Crop catalog greenhouse filter | Invariant | Greenhouse catalog output is not filtered by season and remains deterministically sorted/de-duplicated. |
| Managed-crop batch emission | Invariant | A distinct managed-crop location produces at most one managed-crop batch and no batch is emitted for an empty plan. |

## Extension Compliance

| Extension | Status | Result |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; U-MC-07 only manipulates local game state and saved contract data. |
| Property-Based Testing | Compliant | PBT-01 satisfied by the property table above; properties are pure and carry forward to code-generation planning. |

