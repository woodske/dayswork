# NFR Requirements - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / NFR Requirements
**Status**: Generated from recommended NFR answers on 2026-05-31

## Answered Decisions

| Question | Answer | NFR decision |
|---|---|---|
| NFR-Q1 | A | Route lookup and validation are bounded synchronous operations: table lookup, one requested route per work or deposit attempt, no generic Content Patcher graph scan, and no repeated full-map route discovery in hot paths. |
| NFR-Q2 | A | Shift-time work routes and expansion deposit routes revalidate live readiness on every attempt. Passability is not cached across days, saves, or location reloads. |
| NFR-Q3 | A | Route failures are total and non-throwing, skip only the affected shed greenhouse batch or deposit trip, preserve items, and emit one maintainer warning with route id, purpose, target, first failing hop if known, and reason. |
| NFR-Q4 | A | Automated coverage requires examples plus FsCheck properties with domain generators for route definitions, requests, failures, policy decisions, destination filtering, and item-safety mapping. Shrinking and reproducibility remain enabled. |
| NFR-Q5 | A | TODO-10 reuses the existing C#/.NET, SMAPI/Stardew API, movement/navigation, xUnit, and FsCheck stack. No new runtime dependency, route-graph package, or Content Patcher parser is introduced. |
| NFR-Q6 | A | Closure requires at least one full live SVE playtest on a supported farm map; other supported farm maps may be covered by source-grounded route data and automated tests unless playtest time is available. |

## Performance and Scalability

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-01 | Route lookup must be table-based and bounded by the small configured route set for the active profile and live farm signature. | Unit tests cover deterministic lookup for supported and unsupported signatures. |
| NFR-T10-02 | Shift-time route validation must validate only the requested work or deposit route once per attempt. It must not perform generic graph discovery, full-map scanning, or repeated discovery in hot paths. | Code review and tests confirm validation receives an explicit route request and returns one total result. |
| NFR-T10-03 | Discovery surfaces may use route-shape checks when menus open, but route-shape discovery must not inspect SVE quest, event, or mail flags and must not be reused as proof of shift readiness. | UI discovery tests distinguish route-shape availability from shift readiness. |
| NFR-T10-04 | The design does not need async work, background workers, or caching to scale; the TODO-10 route set is small and per-attempt validation is the intended performance envelope. | NFR Design must keep the synchronous SMAPI runtime model. |

## Availability and Freshness

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-05 | Every shed-greenhouse work route and expansion deposit route must revalidate live location, tile, passability, and reachability state immediately before the worker starts route movement. | Example tests cover stale discovery followed by shift-time failure. |
| NFR-T10-06 | Passability and readiness results must not be trusted across day changes, saves, reloads, map reloads, or changing SVE shed state. | Runtime code avoids day-long and save-long passability caches. |
| NFR-T10-07 | Unsupported farm signatures, missing live locations, invalid route data, and unreachable approach tiles must degrade to safe unavailability or route failure rather than player-visible breakage. | Tests cover unsupported signature and missing route/location failure categories. |

## Reliability and Item Safety

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-08 | Route lookup, validation, and policy mapping must be total: failures return typed result values and reasons instead of throwing for expected world-state absence. | FsCheck properties generate valid and invalid route models and assert success or typed failure only. |
| NFR-T10-09 | Work-route failure must skip only the affected `Custom_GrandpasShedGreenhouse` batch and continue remaining eligible work. It must not cancel unrelated scopes or mark the contract needs-attention. | Example and property tests cover skip scope boundaries. |
| NFR-T10-10 | Deposit-route failure must preserve all in-flight items by mapping them to the existing undelivered or overflow paths. Item id, quantity, source, and provenance must be preserved. | Item-safety properties generate item stacks and assert preserved identity and counts. |
| NFR-T10-11 | Route failure logging must emit one maintainer-facing warning per failed route attempt, not one warning per probe or tile. | Tests or review verify warning aggregation around route attempts. |
| NFR-T10-12 | Warning payloads must include route id, purpose, target, first failing hop when known, and reason. | Example tests cover a representative failed hop. |

## Maintainability and Compatibility

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-13 | SVE route ids, location names, supported farm signatures, route purposes, and hop coordinates must remain centralized in the profile or route model. General runtime code must not scatter SVE strings. | Code review checks SVE-specific constants are confined to the compat/profile layer. |
| NFR-T10-14 | The Vanilla profile must expose no TODO-10 routes or virtual shed greenhouse locations. Vanilla behavior and non-shed SVE contracts remain unchanged. | Regression tests cover vanilla/no-selection behavior. |
| NFR-T10-15 | TODO-10 must not change save DTOs or contract persistence shape. The selected shed greenhouse continues to use the existing greenhouse location string. | Serialization and contract model review confirm no schema migration. |
| NFR-T10-16 | Future expansion support should be possible through new profile route data and descriptors, not new branches in general worker, UI, or deposit code. | NFR Design should define profile-driven pattern boundaries. |

## Usability and Manual Verification

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-17 | No new player-facing route-unavailable HUD message, mail, or error UI is introduced. Existing overflow settlement mail remains allowed when items cannot be delivered. | Manual and example scenarios check that route unavailability is maintainer-facing only. |
| NFR-T10-18 | Existing hiring, greenhouse-selection, destination-selection, and shift flows remain the player experience. The shed greenhouse appears as an alternative greenhouse only when discovery availability succeeds. | UI discovery tests cover standard greenhouse versus shed greenhouse selection. |
| NFR-T10-19 | Before closing TODO-10, perform at least one full live SVE playtest on a supported farm map that selects the shed greenhouse, reaches it through the multi-hop route, performs crop work, deposits or exits item-safely, and verifies no player-facing route-error UI. | Build and Test instructions must include this playtest scenario. |
| NFR-T10-20 | IF2R, Grandpa's Farm, and Frontier Farm route data must be source-grounded. Farm maps not covered by live playtest require source-grounded route data plus automated route tests. | Route table tests reference the supported signatures and route definitions. |

## Security

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-21 | Security Baseline is disabled for TODO-10. The unit introduces no network access, authentication, authorization, secrets, PII, or external process boundary. | No security-specific artifact is required; keep the no-new-surface claim true during Code Generation. |

## Test Requirements

| Requirement | Statement | Verification expectation |
|---|---|---|
| NFR-T10-22 | Example tests must pin the business-critical shed-greenhouse scenarios: supported route selection, unsupported route skip, deposit failure item preservation, vanilla invariance, and destination filtering. | Code Generation plan must include example-test steps for these scenarios. |
| NFR-T10-23 | FsCheck properties must cover documented invariants for route lookup, hop order, no direct shortcut, total validation, policy decisions, destination filtering, and item-safety mapping. | Property test classes use domain-specific generators and avoid raw primitive-only inputs. |
| NFR-T10-24 | Domain generators must cover route definitions, route requests, farm signatures, route purposes, hop lists, failure reasons, policy outcomes, destination descriptors, and item stacks. | Generator utilities are reusable where multiple properties share the same domain types. |
| NFR-T10-25 | FsCheck shrinking and reproducibility must remain enabled through the existing xUnit/FsCheck integration. | Tests must not disable shrinking; Build and Test instructions must include seed/reproducibility expectations. |

## Extension Compliance

| Extension | Status | NFR Requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. Disabled in TODO-10 requirements; no network, auth, secrets, or PII surface is introduced. |
| Property-Based Testing | Enabled - Partial | Compliant. PBT-02 is N/A unless Code Generation introduces reversible serialization/parsing. PBT-03 invariants are required by NFR-T10-23. PBT-07 generators are required by NFR-T10-24. PBT-08 shrinking and reproducibility are required by NFR-T10-25. PBT-09 is satisfied by selecting FsCheck in the tech stack decisions. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
