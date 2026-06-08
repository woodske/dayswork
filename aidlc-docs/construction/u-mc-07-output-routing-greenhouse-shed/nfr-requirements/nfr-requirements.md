# U-MC-07 NFR Requirements

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Review required

## Scope

U-MC-07 completes the final Manage Crops runtime and authoring behaviors: per-zone harvest output routing, season-agnostic greenhouse/SVE-shed crop groups, live-location field reading, and location-aware managed-crop batches. It must preserve ordinary `HarvestCrops` routing, existing shopping behavior, existing deposit safety, and vanilla/no-SVE behavior unless the player explicitly authors managed-crop greenhouse or shed groups.

## Performance

| ID | Requirement |
|---|---|
| PERF-MC7-01 | Managed-crop provenance destination maps must be built from the current crop-plan assignments in O(number of assignments) time. |
| PERF-MC7-02 | Deposit planning must resolve managed-crop provenance destinations with O(1) dictionary lookup per buffered stack and must not add route searches inside the pure planner. |
| PERF-MC7-03 | Managed-crop batch emission must group by distinct location in O(number of assignments) time and avoid scanning unrelated farm or map content. |
| PERF-MC7-04 | `ManagedCropFieldReader` must read only the tiles in assigned managed zones for the active location; live `Diggable` checks must not trigger whole-map traversal on the shift hot path. |
| PERF-MC7-05 | Greenhouse and SVE shed entry/return must reuse existing route descriptors and navigation paths instead of performing new dynamic graph discovery per action. |
| PERF-MC7-06 | Season-agnostic crop catalog reads should occur on authoring/picker interactions, not repeatedly inside per-tile runtime action loops. |

## Scalability

| ID | Requirement |
|---|---|
| SCALE-MC7-01 | The model must support multiple managed-crop groups, multiple zones per group, and multiple distinct locations in one contract. |
| SCALE-MC7-02 | Same tile coordinates in different locations must remain independent for zone exclusion, field-state reads, and provenance-key generation. |
| SCALE-MC7-03 | Distinct managed-crop locations must produce stable, de-duplicated batches, with no duplicate batch for repeated assignments in the same location. |
| SCALE-MC7-04 | Per-zone output routing must scale with buffered output count and assignment count, not with total inventory size across all chests in the world. |
| SCALE-MC7-05 | PBT generators must cover empty, single-location, multi-location, assigned-chest, and automatic-fallback crop-plan shapes without unbounded collections. |

## Reliability and Resilience

| ID | Requirement |
|---|---|
| REL-MC7-01 | A managed-crop zone with an assigned output chest must route harvested output to that chest when the chest is resolvable and reachable. |
| REL-MC7-02 | A managed-crop zone without an assigned output chest must preserve today's automatic output fallback to the farmhand office output chest or overflow path. |
| REL-MC7-03 | Missing, busy, full, or unreachable assigned output chests must use the existing overflow/mail safety behavior without losing quantity. |
| REL-MC7-04 | Managed-crop provenance must take precedence only for matching managed-crop harvested items; ordinary non-managed `HarvestCrops` items must continue through task-level destinations. |
| REL-MC7-05 | Managed-crop provenance keys must be deterministic for the same persisted assignment identity and distinct for different location/zone/group identities. |
| REL-MC7-06 | Missing greenhouse or SVE shed routes must skip only the affected managed-crop batch with diagnostics; the rest of the shift must continue. |
| REL-MC7-07 | After shopping for a non-farm managed-crop batch, the worker must re-enter the active greenhouse/shed location before replanning supply-dependent crop work. |
| REL-MC7-08 | Season-agnostic viability bypass must not bypass seed/fertilizer atomicity, tool/capability checks, energy checks, live occupancy checks, or live `Diggable` checks. |

## Compatibility

| ID | Requirement |
|---|---|
| COMPAT-MC7-01 | No save-envelope or DTO schema bump is required; U-MC-07 reuses existing crop-plan assignment fields, `Mode`, `Zone.LocationName`, `OutputChest`, and `GroupId`. |
| COMPAT-MC7-02 | Existing crop groups hydrate as farm seasonal groups by default and must not be silently reinterpreted as greenhouse or shed groups. |
| COMPAT-MC7-03 | Vanilla/no-SVE behavior must remain unchanged unless a contract explicitly contains greenhouse managed-crop assignments. |
| COMPAT-MC7-04 | SVE shed greenhouse behavior must remain isolated behind the existing expansion profile and route descriptors. |
| COMPAT-MC7-05 | Ordinary `WaterCrops`, ordinary `HarvestCrops`, fruit, animal, clearing, shopping, and existing deposit behavior must not change outside the managed-crop routing additions. |
| COMPAT-MC7-06 | Existing chest destination, automatic output, and shipping-bin destination types must be reused rather than replaced. |

## Maintainability

| ID | Requirement |
|---|---|
| MAINT-MC7-01 | Keep destination precedence in a small pure seam: managed-crop provenance destinations before task-level destinations before automatic fallback. |
| MAINT-MC7-02 | Preserve the existing `DepositPlanner` overload for current callers and add a provenance-aware overload instead of forcing unrelated call sites to know about managed crops. |
| MAINT-MC7-03 | Keep greenhouse/shed classification explicit and centralized near the existing location/expansion profile boundaries. |
| MAINT-MC7-04 | Keep crop-group authoring changes inside the existing `ManageCropsMenu`, `CropGroupEditorMenu`, `CropListPickerMenu`, and zone draw components. |
| MAINT-MC7-05 | Keep live Stardew APIs in Mod-layer adapters and runners; pure Core tests should exercise provenance maps, planner precedence, batch grouping, and draft projection without SMAPI dependencies. |
| MAINT-MC7-06 | All new player-facing labels, HUD notices, and menu text must be i18n-backed and pass the hardcoded-string lint gate. |

## Security and Privacy

| ID | Requirement |
|---|---|
| SEC-MC7-01 | Security Baseline is disabled for Manage Crops; no Security Baseline checks are blocking in this stage. |
| SEC-MC7-02 | U-MC-07 introduces no network I/O, authentication, authorization, secrets, PII, external service calls, or external process execution. |
| SEC-MC7-03 | Saved contract data and live map data must continue to be treated defensively: invalid locations, invalid chests, and unavailable routes degrade to skips or overflow paths instead of throwing. |

## Availability

| ID | Requirement |
|---|---|
| AVAIL-MC7-01 | U-MC-07 has no service uptime, failover, or disaster-recovery requirement because it is local SMAPI mod logic. |
| AVAIL-MC7-02 | Runtime availability is local resilience: one bad greenhouse/shed route, missing chest, or invalid assignment must not abort the whole shift. |
| AVAIL-MC7-03 | The feature must remain opt-in; contracts without managed-crop assignments must continue existing shift behavior. |

## Usability

| ID | Requirement |
|---|---|
| USE-MC7-01 | The crop-group editor must expose only available managed-crop locations: Farm always, Greenhouse when available, and Grandpa's Shed Greenhouse only when the active expansion profile supports it. |
| USE-MC7-02 | Changing a crop group's location must clear zones so stale coordinates are not silently reused in another location. |
| USE-MC7-03 | Season-agnostic groups must show one year-round crop row; farm groups must keep the current seasonal row layout. |
| USE-MC7-04 | Output chest selection must keep an automatic fallback option while excluding built-in office supply/output chests from explicit per-zone destination choices. |

## Test Rigor

| ID | Requirement |
|---|---|
| TEST-MC7-01 | Code generation must include example tests for managed-crop destination precedence, automatic fallback, ordinary harvest routing preservation, chest failure fallback, greenhouse viability bypass, and location-scoped zone exclusion. |
| TEST-MC7-02 | Code generation must include FsCheck properties for provenance key equality/distinction, destination-map construction, provenance-before-task routing, season-agnostic draft projection/hydration, managed-location batch grouping, crop-catalog greenhouse filtering, and live-field classification seams. |
| TEST-MC7-03 | PBT generators must be domain-specific and reusable for crop assignments, zones, locations, output chests, provenance keys, buffered items, destination maps, and season-agnostic draft shapes. |
| TEST-MC7-04 | FsCheck shrinking and seed reproducibility must remain enabled through the existing xUnit/FsCheck integration. |
| TEST-MC7-05 | PBT must complement example tests; it must not be the only coverage for per-zone output routing, greenhouse authoring, or live route failure behavior. |
| TEST-MC7-06 | Manual SMAPI playtest coverage remains required for visible greenhouse and SVE shed greenhouse authoring, entry, crop work, shopping re-entry, and deposit paths. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Previously satisfied | Functional Design artifacts identify U-MC-07 properties and carry them forward. |
| PBT-02 | Required for code generation | Season-agnostic draft projection/hydration and any DTO/domain round-trip touched by U-MC-07 must be property-tested. |
| PBT-03 | Required for code generation | Destination precedence, batch grouping, location scoping, greenhouse catalog filtering, and field classification invariants must be property-tested. |
| PBT-04 | N/A | U-MC-07 does not claim idempotent normalization beyond existing crop-plan hydration behavior. |
| PBT-05 | N/A | No optimized algorithm or independent reference implementation is introduced. |
| PBT-06 | N/A | U-MC-07's property-applicable seams are pure transformations; live orchestrator state remains example/playtest-covered adapter behavior. |
| PBT-07 | Required for code generation | Domain-specific generators are mandatory for provenance, assignment, destination, and season-agnostic draft properties. |
| PBT-08 | Required for code generation/build-test | FsCheck shrinking and seed reproducibility must remain enabled. |
| PBT-09 | Compliant | FsCheck.Xunit is selected and present in `Dayswork.Tests.csproj`. |
| PBT-10 | Required for code generation | Example tests must accompany PBT for critical routing and greenhouse/shed behavior. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Full-mode rules are evaluated; PBT-09 is satisfied at NFR Requirements and downstream obligations are explicit. |

