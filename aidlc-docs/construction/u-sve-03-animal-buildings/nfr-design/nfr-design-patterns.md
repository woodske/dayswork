# NFR Design Patterns — U-SVE-03 SVE Animal Buildings

Patterns realizing the approved NFR requirements (NFRU3-01..08) on the existing component seams. No new infrastructure.

## P-SVE3-01 — Data-driven capacity via existing pure policy (clamp-not-throw)
Feed capacity is derived by the pure `AnimalBuildingCapacityPolicy.DeriveCapacity` = `min(troughTiles, MaxOccupants)`, consumed through `ExpansionCompatService.ResolveAnimalFeedCapacity`. The policy is total (negative/zero-safe) so it never throws (NFRU3-03). Replaces the hardcoded `FeedCapacity` ladder in `AnimalTaskHandler`.
- Realizes: NFRU3-01, NFRU3-03, NFRU3-04.

## P-SVE3-02 — Auto-feed gate precedence (short-circuit before scan)
`AnimalTaskHandler.CreateFeedWork` keeps the existing auto-feed gate (`IsAutoFeedBuilding`, incl. the `AutoFeed = T` map property) **ahead of** capacity derivation, so SVE premium and vanilla Deluxe buildings return no feed work without ever scanning troughs.
- Realizes: NFRU3-01 (performance short-circuit); preserves vanilla feeding semantics.

## P-SVE3-03 — Premium→tier strategy table, profile-first precedence
`SveExpansionProfile.MapPremiumBuildingTier` holds the premium `buildingType → Deluxe*` table (O(1) lookup). The hiring enumeration consults it via `ExpansionCompatService.ResolveAnimalBuildingTier(building, vanillaTier)` **before** the vanilla substring inference; a non-null result wins, otherwise the vanilla path runs unchanged. All SVE ids stay inside the profile.
- Realizes: NFRU3-02, NFRU3-04, NFRU3-07.

## P-SVE3-04 — Null-Object vanilla profile (vanilla invariance)
With the Vanilla profile, `MapPremiumBuildingTier` returns `null` for all inputs and data-driven capacity equals the legacy ladder for vanilla buildings (parity asserted at code-gen). Vanilla farms are byte-for-byte unchanged.
- Realizes: NFRU3-06.

## P-SVE3-05 — Pure-Core seams + thin adapter + FsCheck seam
Decision logic (`DeriveCapacity`, `MapPremiumBuildingTier`) lives in pure `Dayswork.Core` and is FsCheck-tested without SMAPI; `ExpansionCompatService` is the only SMAPI-touching adapter (trough scan, live building/`MaxOccupants` reads), validated by playtest.
- Realizes: NFRU3-05, NFRU3-08, NFRU3-03.

## Extension compliance
| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A |
| Property-Based Testing | Enabled, full | Compliant — P-SVE3-01/03/05 carry the FsCheck obligations (capacity invariants, tier-map totality/pass-through) into Code Generation. |
