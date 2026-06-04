# NFR Design Plan - U-MC-01 Crop-plan Domain + Persistence Foundation

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Complete - review required

## Context

This stage incorporates approved NFR Requirements into implementation patterns and logical components for U-MC-01.

Inputs:

- Approved Functional Design at `construction/u-mc-01-domain-persistence/functional-design/`.
- Approved NFR Requirements at `construction/u-mc-01-domain-persistence/nfr-requirements/`.
- Existing project stack: `Dayswork.Core`, .NET 6, C# 10, Newtonsoft.Json, xUnit, FsCheck.Xunit 2.16.5.
- Manage Crops extension status: Security Baseline disabled; Property-Based Testing enabled in full mode.

## Question Category Evaluation

No additional question round was issued. All mandatory NFR Design categories were evaluated:

| Category | Evaluation | Question needed? | Rationale |
|---|---|---|---|
| Resilience Patterns | Applicable | No | Approved NFRs already select missing-plan defaulting, per-contract skip-and-warn, explicit no-action/no-store outcomes, and seed/fertilizer atomicity. |
| Scalability Patterns | Applicable | No | Bounds are local and input-size based: assignments, tile candidates, seasons, and two stores. No unbounded service or farm-wide graph scaling pattern is needed. |
| Performance Patterns | Applicable | No | NFRs already require pure in-memory planners, no live map traversal, no pathfinding, deterministic sorted DTO output, and O(input-size) operations. |
| Security Patterns | N/A | No | Security Baseline is disabled and U-MC-01 adds no network, auth, PII, secrets, or external-service surface. Defensive parsing remains a reliability pattern. |
| Logical Components | Applicable | No | Functional Design and NFR Requirements already identify the exact pure components and persistence mapper. No queues, caches, circuit breakers, or infrastructure components apply. |

## Plan Checklist

- [x] Load NFR Design rule details from `.aidlc-rule-details/construction/nfr-design.md`.
- [x] Load content-validation rules and confirm generated content uses plain Markdown with no Mermaid or ASCII diagrams.
- [x] Load approved U-MC-01 NFR Requirements and tech-stack decisions.
- [x] Load approved U-MC-01 Functional Design context.
- [x] Evaluate all mandatory NFR Design question categories.
- [x] Determine no additional question round is needed and record rationale.
- [x] Generate `construction/u-mc-01-domain-persistence/nfr-design/nfr-design-patterns.md`.
- [x] Generate `construction/u-mc-01-domain-persistence/nfr-design/logical-components.md`.
- [x] Update `aidlc-state.md` to the U-MC-01 NFR Design review gate.
- [x] Log approval/start and completion in `audit.md`.
- [x] Present the standardized NFR Design completion message and wait for explicit approval.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Design carries PBT obligations into the generator/test-support component pattern for Code Generation. |

