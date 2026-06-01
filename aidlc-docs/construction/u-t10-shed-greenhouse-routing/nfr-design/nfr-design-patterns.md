# NFR Design Patterns - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Design
**Status**: Generated on 2026-05-31

## Pattern Summary

| Pattern | Category | Purpose |
|---|---|---|
| P-T10-NFR-01 Profile-owned explicit route table | Maintainability / performance | Keep SVE route data centralized, source-grounded, and lookup-bounded. |
| P-T10-NFR-02 Discovery shape vs shift readiness split | Availability / correctness | Let menus discover possible shed greenhouse support without treating discovery as runtime readiness. |
| P-T10-NFR-03 Per-attempt live validation adapter | Resilience / freshness | Revalidate live route state immediately before work and deposit movement. |
| P-T10-NFR-04 Validated ordered-hop navigator | Performance / separation | Execute already validated route hops without owning policy or doing generic graph discovery. |
| P-T10-NFR-05 Orchestrator-owned route failure policy | Resilience / compatibility | Keep skip, continue, deposit failure, warning, and no-player-error decisions in `ShiftOrchestrator`. |
| P-T10-NFR-06 Item-safe deposit failure barrier | Reliability | Preserve every in-flight item through existing undelivered and overflow paths. |
| P-T10-NFR-07 Draft-aware expansion destination filter | Usability / compatibility | Offer shed greenhouse and main-shed chests only for selected shed-greenhouse output. |
| P-T10-NFR-08 One-warning route failure aggregation | Maintainability | Emit one maintainer-facing warning per failed route attempt with route context. |
| P-T10-NFR-09 Pure route-property test seam | Testability | Keep route lookup, invariants, policy mapping, filtering, and item mapping property-testable with FsCheck. |
| P-T10-NFR-10 Manual SVE playtest checkpoint | Operational verification | Require one end-to-end live SVE playtest for the multi-hop shed greenhouse path before TODO-10 closes. |

## P-T10-NFR-01 - Profile-Owned Explicit Route Table

SVE route ids, supported farm signatures, source and target location names, route purposes, hop ordinals, approach tiles, and arrival tiles remain in `SveExpansionProfile` or adjacent profile-owned Core route data. The Vanilla profile returns no expansion routes or expansion shed greenhouse descriptors.

This pattern satisfies NFR-T10-01, NFR-T10-13, NFR-T10-14, and NFR-T10-16 by avoiding scattered SVE strings, generic graph discovery, and vanilla behavior changes.

## P-T10-NFR-02 - Discovery Shape vs Shift Readiness Split

Discovery availability answers whether the UI may show the shed greenhouse as an alternative greenhouse and eligible shed/main-shed chest destinations. Shift readiness answers whether the worker may start movement right now. Discovery may validate route shape when menus open; shift readiness revalidates live location, tile, passability, and reachability state immediately before movement.

This pattern satisfies NFR-T10-03, NFR-T10-05, NFR-T10-06, NFR-T10-07, and NFR-T10-18. It also prevents SVE quest, event, or mail flags from becoming scheduling authority.

## P-T10-NFR-03 - Per-Attempt Live Validation Adapter

`ExpansionCompatService` acts as the live-world adapter. It builds a route request from the active profile, current farm signature, source location, target location, and route purpose; resolves live locations and tiles; checks bounds, passability, and worker reachability; and returns a total validation result.

Expected absence and invalid state return typed failures rather than exceptions. This pattern satisfies NFR-T10-02, NFR-T10-05, NFR-T10-06, NFR-T10-07, and NFR-T10-08.

## P-T10-NFR-04 - Validated Ordered-Hop Navigator

`CrossLocationRouteNavigator` executes validated route hops in order. For each hop, it walks the worker to the validated approach tile, performs the configured transition to the target location, places the worker at the validated arrival tile, and advances to the next hop.

The navigator does not perform broad route discovery and does not decide skip, continue, mail, overflow, or contract state. This pattern satisfies NFR-T10-02, NFR-T10-04, and NFR-T10-08 while preserving the policy boundary from Functional Design.

## P-T10-NFR-05 - Orchestrator-Owned Route Failure Policy

`ShiftOrchestrator` remains the decision owner for route outcomes. Work-route failure maps to skipping only the affected shed greenhouse batch and continuing remaining work. Deposit-route failure maps to the existing undelivered or overflow handling for that deposit trip. Route unavailability does not create new player-facing route-error UI or needs-attention contract state.

This pattern satisfies NFR-T10-09, NFR-T10-10, NFR-T10-11, NFR-T10-12, and NFR-T10-17.

## P-T10-NFR-06 - Item-Safe Deposit Failure Barrier

Expansion deposit trips treat route validation failure, navigation failure, missing chest, full chest, and stand-tile failure as delivery failures, not item-loss events. Existing undelivered and overflow paths carry item id, quantity, source, and provenance forward.

This pattern satisfies NFR-T10-10 and connects the route feature to the project's existing item-safety behavior instead of introducing a separate storage path.

## P-T10-NFR-07 - Draft-Aware Expansion Destination Filter

UI and destination discovery use the current contract draft or selected scope to decide whether expansion chests are eligible. Chests in `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` are exposed only when the selected greenhouse work location is `Custom_GrandpasShedGreenhouse`. `Custom_GrandpasShed` never becomes a work-scope selection.

This pattern satisfies NFR-T10-14, NFR-T10-18, and the functional destination rules BR-T10-17 through BR-T10-21.

## P-T10-NFR-08 - One-Warning Route Failure Aggregation

Each failed route attempt emits at most one maintainer-facing warning containing route id, purpose, target, first failing hop if known, and failure reason. Tile probes, hop validation details, and repeated checks inside the same attempt do not each emit independent warnings.

This pattern satisfies NFR-T10-11 and NFR-T10-12 while keeping route failures diagnosable without flooding logs.

## P-T10-NFR-09 - Pure Route-Property Test Seam

The implementation keeps route lookup, route definition invariants, policy mapping, expansion destination filtering, and item-safety mapping in pure or pure-adjacent seams wherever practical. Code Generation must use example tests for business-critical scenarios and FsCheck properties with domain-specific generators for the documented invariants.

This pattern satisfies NFR-T10-22 through NFR-T10-25 and the enabled Property-Based Testing partial-mode rules PBT-03, PBT-07, PBT-08, and PBT-09. PBT-02 remains N/A unless Code Generation introduces reversible serialization, parsing, or formatting behavior.

## P-T10-NFR-10 - Manual SVE Playtest Checkpoint

Build and Test must include a live SVE playtest on at least one supported farm map. The playtest selects the shed greenhouse, reaches it through the explicit multi-hop route, performs greenhouse crop work, deposits or exits item-safely, and confirms no new player-facing route-error UI appears.

This pattern satisfies NFR-T10-19 and complements automated source-grounded route-table coverage for the other supported farm maps.

## Pattern to NFR Map

| Pattern | Primary NFR coverage |
|---|---|
| P-T10-NFR-01 | NFR-T10-01, NFR-T10-13, NFR-T10-14, NFR-T10-16 |
| P-T10-NFR-02 | NFR-T10-03, NFR-T10-05, NFR-T10-06, NFR-T10-07, NFR-T10-18 |
| P-T10-NFR-03 | NFR-T10-02, NFR-T10-05, NFR-T10-06, NFR-T10-07, NFR-T10-08 |
| P-T10-NFR-04 | NFR-T10-02, NFR-T10-04, NFR-T10-08 |
| P-T10-NFR-05 | NFR-T10-09, NFR-T10-10, NFR-T10-11, NFR-T10-12, NFR-T10-17 |
| P-T10-NFR-06 | NFR-T10-10 |
| P-T10-NFR-07 | NFR-T10-14, NFR-T10-18 |
| P-T10-NFR-08 | NFR-T10-11, NFR-T10-12 |
| P-T10-NFR-09 | NFR-T10-22, NFR-T10-23, NFR-T10-24, NFR-T10-25 |
| P-T10-NFR-10 | NFR-T10-19, NFR-T10-20 |

## Extension Compliance

| Extension | Status | NFR Design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. No security pattern is applicable because TODO-10 introduces no network, auth, secrets, PII, or external boundary. |
| Property-Based Testing | Enabled - Partial | Compliant. P-T10-NFR-09 carries PBT-03/PBT-07/PBT-08 into Code Generation and Build/Test, PBT-09 remains satisfied by FsCheck, and PBT-02 remains N/A unless reversible transforms are introduced. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
