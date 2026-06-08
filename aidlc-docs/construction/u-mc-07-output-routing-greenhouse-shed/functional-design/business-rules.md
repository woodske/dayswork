# Business Rules - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

Rules are prefixed `BR-MC7-*`.

## Per-Zone Output Routing

- **BR-MC7-01** Managed-crop harvest output uses the owning `CropZoneAssignment.OutputChest` when it is assigned. (FR-MC-29, S-31)
- **BR-MC7-02** A managed-crop zone with no assigned output chest uses the current automatic output fallback, preserving the farmhand office output-chest behavior. (FR-MC-29)
- **BR-MC7-03** Per-zone managed-crop routing is resolved before task-level `HarvestCrops` routing so ordinary harvest destinations do not override zone-specific crop output. (FR-MC-28, FR-MC-29)
- **BR-MC7-04** Ordinary non-managed `HarvestCrops` output continues to use the existing task-level destination map and is not affected by managed-crop provenance rules. (FR-MC-28)
- **BR-MC7-05** Managed-crop harvest items retain `TaskKind.HarvestCrops` for overflow categorization, but carry managed-crop provenance for destination selection. (FR-MC-29)
- **BR-MC7-06** Assigned chest failures - missing chest, busy chest, full chest, or unreachable route - use the existing overflow/mail behavior. No harvested item is lost. (NFR-MC-03, NFR-MC-05)
- **BR-MC7-07** Per-zone output chest lists exclude built-in office supply/output chests from player-selectable managed-crop destinations, while null output chest remains available as the office-output fallback. (FR-MC-35)

## Season-Agnostic Authoring

- **BR-MC7-08** A crop group in `Farm` mode remains seasonal and uses the existing four-season editor. (FR-MC-02, FR-MC-04)
- **BR-MC7-09** A crop group in vanilla greenhouse or SVE shed-greenhouse mode is season-agnostic and shows one continuous crop assignment instead of seasonal rows. (FR-MC-05, S-32)
- **BR-MC7-10** Season-agnostic crop pickers do not filter by season; every crop from the live crop catalog is eligible unless the catalog cannot map it. (FR-MC-03, FR-MC-05)
- **BR-MC7-11** Season-agnostic assignments persist as `CropAssignmentMode.SeasonAgnostic`; the existing `SeasonCropChoice.Season` value is an ignored carrier for DTO compatibility. (FR-MC-05, FR-MC-38)
- **BR-MC7-12** Existing seasonal crop groups hydrate as farm seasonal groups; existing save data is not reinterpreted as greenhouse/shed data. (NFR-MC-06)

## Greenhouse/Shed Runtime

- **BR-MC7-13** `ShiftPlanBuilder` emits a managed-crop batch for every distinct managed-crop location, including `Farm`, `Greenhouse`, and supported expansion greenhouse locations. (FR-MC-43)
- **BR-MC7-14** Managed-crop batches run before general crop work for the same location, preventing duplicate harvest/water actions inside managed zones. (FR-MC-28)
- **BR-MC7-15** The managed-crop runtime reads and mutates the active batch location, not always `Game1.getFarm()`. (FR-MC-43)
- **BR-MC7-16** Greenhouse and shed-greenhouse field states bypass the end-of-season viability gate but still enforce seed/fertilizer atomicity and item availability. (FR-MC-23, FR-MC-11)
- **BR-MC7-17** Plantable/tillable candidates are determined from the live location's `Diggable` Back-layer property at shift time. No greenhouse/shed plantable region is hardcoded. (FR-MC-44)
- **BR-MC7-18** SVE `Custom_GrandpasShedGreenhouse` uses the existing expansion route provider; no new hardcoded navigation path is introduced outside `SveExpansionProfile`. (FR-MC-43, NFR-MC-04)
- **BR-MC7-19** SVE `Custom_GrandpasShed` proper remains `DepositOnly`; crop work occurs in `Custom_GrandpasShedGreenhouse`. (FR-MC-43)
- **BR-MC7-20** If a greenhouse/shed route is missing or invalid, the batch skips safely with diagnostics and the rest of the shift continues. (NFR-MC-05)

## Shopping and Supply

- **BR-MC7-21** Input-chest supply remains the first source for all managed-crop locations. (FR-MC-34)
- **BR-MC7-22** Shopping can serve season-agnostic locations using the existing U-MC-06 stock, price, affordability, and item/gold safety rules. (FR-MC-12..18)
- **BR-MC7-23** After a shopping trip for a non-farm managed-crop batch, the worker returns to the active greenhouse/shed location before replanning and planting. (FR-MC-17, FR-MC-43)
- **BR-MC7-24** Shopping continues to cost time only and never spends energy. (FR-MC-41)

## Coexistence and Safety

- **BR-MC7-25** General `WaterCrops` and `HarvestCrops` remain available outside managed zones in the same location. (FR-MC-28)
- **BR-MC7-26** Managed-zone exclusion applies by location; a tile in `Greenhouse` does not exclude a same-coordinate tile on `Farm` or another location. (FR-MC-28)
- **BR-MC7-27** All new player-facing text is i18n-backed and must pass the hardcoded-string lint gate. (NFR-MC-07)
- **BR-MC7-28** Vanilla/no-SVE behavior is unchanged unless a crop plan explicitly includes greenhouse managed-crop groups. (NFR-MC-04)

## PBT-01 Property Summary

| Rule | Property category | Testable property |
|---|---|---|
| BR-MC7-01..04 | Invariant | Managed-crop destination precedence never changes ordinary harvest routing. |
| BR-MC7-06 | Invariant | Chest failure routes all remaining quantities to overflow/mail; no quantity is dropped. |
| BR-MC7-08..12 | Round-trip / invariant | Season-agnostic draft choices project and hydrate without losing crop/fertilizer/replant/output chest data. |
| BR-MC7-13..14 | Invariant | Managed-crop batch emission is one per distinct location and ordered before general crop work for that location. |
| BR-MC7-16..17 | Invariant | Season-agnostic field states bypass viability while preserving live `Diggable` gating. |
| BR-MC7-25..26 | Invariant | Managed-zone exclusion is location-scoped and disjoint from general work. |

## Extension Compliance

| Extension | Status | Rule impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | PBT-01 properties are identified above and must be carried into Code Generation planning. |

