# Functional Design Plan - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Complete; review required  
**Source context**: Manage Crops requirements FR-MC-05, FR-MC-23, FR-MC-28, FR-MC-29, FR-MC-31, FR-MC-32, FR-MC-35, FR-MC-43, FR-MC-44; stories S-31 and S-32.

## Execution Checklist

- [x] Load common AI-DLC workflow rules and Functional Design stage rules.
- [x] Load enabled extension rules: Property-Based Testing full mode; Security Baseline skipped because disabled for Manage Crops.
- [x] Read U-MC-07 unit definition, dependency summary, and story map.
- [x] Read Manage Crops requirements, user stories, and application design context.
- [x] Read U-MC-05/U-MC-06 functional design and code summaries to preserve existing managed-crop runtime boundaries.
- [x] Inspect current code extension points: `CropZoneAssignment.OutputChest`, `OutputScopeProvenance`, `DepositPlanner`, `ManagedCropFieldReader`, `ShiftPlanBuilder`, `ShiftOrchestrator.ManagedCrops`, `ChestResolver`, `CropPlanDraft`, `CropGroupEditorMenu`, `ZoneDrawMenu`, `CropCatalogProvider`, and `SveExpansionProfile`.
- [x] Determine question need.
- [x] Generate Functional Design artifacts.
- [x] Validate content before writing files.
- [x] Update `aidlc-state.md` and `audit.md`.

## Question Assessment

No clarification question file was generated. The approved requirements and prior Manage Crops decisions fix the remaining design choices:

- Per-zone harvest output routes to each `CropZoneAssignment.OutputChest`; null output chest keeps the existing automatic office-output fallback.
- Managed-crop harvest must not change ordinary `HarvestCrops` destination routing outside managed zones, so routing is keyed by managed-crop provenance before falling back to task-level destinations.
- Greenhouse and SVE Grandpa's Shed greenhouse are season-agnostic managed-crop locations; no season-filtered crop rows are shown for those groups.
- Plantable tiles are resolved from the live map's `Diggable` property at runtime, including current map variants.
- SVE shed greenhouse route membership uses the existing expansion profile and route validation seam.

## Generated Artifacts

- `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/functional-design/business-rules.md`
- `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-mc-07-output-routing-greenhouse-shed/functional-design/frontend-components.md`

## Content Validation

- Mermaid diagrams: N/A. No Mermaid diagrams generated.
- ASCII diagrams: N/A. No ASCII diagrams generated.
- Markdown parsing: headings, lists, tables, and inline code are balanced.
- Special characters: code identifiers and paths are wrapped in backticks where needed.
- Text alternatives: N/A because no visual diagrams were generated.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops in `aidlc-state.md`; U-MC-07 adds no network, PII, auth, or external service surface. |
| Property-Based Testing | Compliant | PBT-01 is satisfied by explicit Testable Properties sections in the generated business logic, business rules, and domain entity artifacts. |

