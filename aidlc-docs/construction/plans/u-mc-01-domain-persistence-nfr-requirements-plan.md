# NFR Requirements Plan - U-MC-01 Crop-plan Domain + Persistence Foundation

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Complete - review required

## Context

This stage builds on the approved U-MC-01 Functional Design:

- Additive schema-3 crop-plan persistence.
- Pure Core domain and planners for crop assignment, viability, supply, store resolution, and shift planning.
- Opaque qualified item-ID strings in Core.
- Full-mode PBT obligations using the existing FsCheck/xUnit stack.
- Security Baseline disabled for Manage Crops.

## Question Assessment

No additional NFR question round was issued.

Rationale:

- Scalability and performance are bounded by pure in-memory crop-plan/planner operations over contract zones and generated field-state inputs.
- Availability and disaster recovery are not separate concerns for this local single-player SMAPI mod unit.
- Security Baseline is disabled by approved requirements, and U-MC-01 introduces no network, authentication, authorization, PII, or external service surface.
- Tech stack is fixed by project constraints: .NET 6, C# 10, `Dayswork.Core`, Newtonsoft.Json for persistence, xUnit plus FsCheck.Xunit for tests.
- Reliability expectations are already specified by additive persistence, missing-plan defaulting, per-contract skip-and-warn behavior, and pure deterministic planners.

## Plan Checklist

- [x] Load NFR Requirements rule details from `.aidlc-rule-details/construction/nfr-requirements.md`.
- [x] Load content-validation rules and confirm generated content uses plain Markdown with no Mermaid or ASCII diagrams.
- [x] Load approved U-MC-01 Functional Design artifacts.
- [x] Load enabled extension rules for Property-Based Testing full mode.
- [x] Verify current tech stack and PBT framework from project files.
- [x] Assess whether NFR clarification questions are needed.
- [x] Generate `construction/u-mc-01-domain-persistence/nfr-requirements/nfr-requirements.md`.
- [x] Generate `construction/u-mc-01-domain-persistence/nfr-requirements/tech-stack-decisions.md`.
- [x] Update `aidlc-state.md` to the U-MC-01 NFR Requirements review gate.
- [x] Log approval/start and completion in `audit.md`.
- [x] Present the standardized NFR Requirements completion message and wait for explicit approval.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops by approved requirements. |
| Property-Based Testing | Compliant | PBT-09 framework selection is documented in `tech-stack-decisions.md`; code-generation obligations are carried forward. |

