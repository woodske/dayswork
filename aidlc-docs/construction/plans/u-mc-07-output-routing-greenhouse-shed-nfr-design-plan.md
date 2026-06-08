# NFR Design Plan - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Complete - review required

## Context

This stage incorporates the approved U-MC-07 NFR Requirements into concrete design patterns and logical components.

The approved quality bar requires:

- Managed-crop provenance destinations before task-level destinations before automatic fallback.
- Backward-compatible deposit planning for ordinary callers.
- Stable, location-scoped assignment identity.
- One managed-crop batch per distinct managed-crop location.
- Live-location field reads bounded to assigned zone tiles.
- Fail-soft greenhouse and SVE shed route handling.
- No save schema bump, no runtime package additions, and no new infrastructure.
- Full-mode Property-Based Testing obligations carried into Code Generation.

## Mandatory Category Evaluation

| Category | Evaluation | Question need |
|---|---|---|
| Resilience Patterns | Fail-soft batch skip, existing overflow/mail fallback, and ordinary harvest-routing preservation are fully specified. | No question needed. |
| Scalability Patterns | Assignment-count maps, buffered-stack lookups, and distinct-location batch grouping are fully specified. | No question needed. |
| Performance Patterns | O(assignment), O(buffered stack), O(zone tile), and route-reuse constraints are fully specified. | No question needed. |
| Security Patterns | Security Baseline is disabled and U-MC-07 has no network, auth, PII, secret, or external service surface. | No question needed. |
| Logical Components | Existing Core/Mod boundaries and the required extension points are clear from Functional Design and current code. | No question needed. |

## Question Assessment

No NFR Design question file was generated.

Rationale:

- The NFR Requirements already fix each mandatory pattern category.
- The current code confirms the needed seams: `DepositPlanner` task-level routing, `OutputScopeProvenance`, farm-only managed-crop batches, farm-only `ManagedCropFieldReader`, `ShiftOrchestrator.ManagedCrops`, `ChestResolver`, and expansion route descriptors.
- No user-facing tradeoff remains open at NFR Design depth; the next stage can plan implementation steps directly.

## Plan Checklist

- [x] Load NFR Design rule details from `.aidlc-rule-details/construction/nfr-design.md`.
- [x] Load common AI-DLC workflow, session-continuity, content-validation, and question-format rules.
- [x] Scan extension opt-in files and load enabled Property-Based Testing full-mode rules.
- [x] Confirm Security Baseline is disabled for Manage Crops in `aidlc-state.md`.
- [x] Load U-MC-07 NFR Requirements and tech-stack decisions.
- [x] Load U-MC-07 Functional Design artifacts.
- [x] Inspect current code seams for exact component names and boundaries.
- [x] Evaluate mandatory NFR Design categories.
- [x] Decide whether NFR Design clarification questions are needed.
- [x] Validate generated content uses plain Markdown with no Mermaid or ASCII diagrams.
- [x] Generate `construction/u-mc-07-output-routing-greenhouse-shed/nfr-design/nfr-design-patterns.md`.
- [x] Generate `construction/u-mc-07-output-routing-greenhouse-shed/nfr-design/logical-components.md`.
- [x] Update `aidlc-state.md` to the U-MC-07 NFR Design review gate.
- [x] Log approval/start and completion in `audit.md`.
- [x] Present the standardized NFR Design completion message and wait for explicit approval.

## Content Validation

- Mermaid diagrams: N/A. No Mermaid diagrams generated.
- ASCII diagrams: N/A. No ASCII diagrams generated.
- Markdown parsing: headings, lists, tables, and inline code are balanced.
- Special characters: code identifiers and paths are wrapped in backticks where needed.
- Text alternatives: N/A because no visual diagrams were generated.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops by approved requirements. |
| Property-Based Testing | Compliant | NFR Design carries PBT-01/PBT-02/PBT-03/PBT-07/PBT-08/PBT-10 obligations into Code Generation while keeping live SMAPI adapters example/playtest-covered. |

