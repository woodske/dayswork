# NFR Requirements Plan - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Complete - review required

## Context

This stage builds on the approved U-MC-07 Functional Design:

- Per-zone managed-crop harvest output routing through managed-crop provenance.
- Ordinary non-managed `HarvestCrops` routing preserved through the existing task-level destination map.
- Season-agnostic authoring and runtime support for vanilla `Greenhouse` and SVE `Custom_GrandpasShedGreenhouse`.
- Live map `Diggable` checks for greenhouse/shed plantable area.
- Reuse of existing vanilla interior and expansion route seams.
- Security Baseline disabled for Manage Crops.
- Property-Based Testing enabled in full mode.

## Question Assessment

No additional NFR question round was issued.

Rationale:

- Scalability and performance are bounded by assignment count, buffered output count, and assigned zone tile count; no new global route discovery or per-tile graph search is required.
- Reliability expectations are already fixed by the approved requirements: null output chest uses office-output fallback, explicit chest failures use existing overflow/mail behavior, missing greenhouse/shed routes skip safely, and ordinary harvest routing must not regress.
- Availability and disaster recovery are not separate concerns for this local single-player SMAPI mod unit.
- Security Baseline is disabled by approved Manage Crops requirements, and U-MC-07 introduces no network, authentication, authorization, PII, secrets, or external service surface.
- Tech stack is fixed by the project and prior Manage Crops units: .NET 6, C# 10, SMAPI, xUnit, FsCheck.Xunit, nullable enabled, warnings as errors, and no new runtime dependencies.
- PBT-09 is already satisfied by `FsCheck.Xunit` in `Dayswork.Tests.csproj`; downstream code generation must implement the U-MC-07 properties identified in Functional Design.

## Plan Checklist

- [x] Load NFR Requirements rule details from `.aidlc-rule-details/construction/nfr-requirements.md`.
- [x] Load common AI-DLC workflow, session-continuity, content-validation, and question-format rules.
- [x] Load approved U-MC-07 Functional Design artifacts.
- [x] Load Manage Crops requirements and execution-plan context.
- [x] Load enabled extension rules for Property-Based Testing full mode.
- [x] Confirm Security Baseline is disabled for Manage Crops in `aidlc-state.md`.
- [x] Verify current .NET, xUnit, and FsCheck.Xunit project stack from project files.
- [x] Assess whether NFR clarification questions are needed.
- [x] Validate generated content uses plain Markdown with no Mermaid or ASCII diagrams.
- [x] Generate `construction/u-mc-07-output-routing-greenhouse-shed/nfr-requirements/nfr-requirements.md`.
- [x] Generate `construction/u-mc-07-output-routing-greenhouse-shed/nfr-requirements/tech-stack-decisions.md`.
- [x] Update `aidlc-state.md` to the U-MC-07 NFR Requirements review gate.
- [x] Log approval/start and completion in `audit.md`.
- [x] Present the standardized NFR Requirements completion message and wait for explicit approval.

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
| Property-Based Testing | Compliant | PBT-09 framework selection is documented in `tech-stack-decisions.md`; PBT-01 properties from Functional Design are carried forward as code-generation obligations. |

