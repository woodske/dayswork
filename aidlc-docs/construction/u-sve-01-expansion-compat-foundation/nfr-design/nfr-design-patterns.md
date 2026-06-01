# NFR Design Patterns — U-SVE-01 Expansion-Compatibility Provider Foundation

Concrete patterns that satisfy the U-SVE-01 NFR requirements (NFRU-01..10) and the change-level `NFR-SVE-*`. Scalability and security patterns are N/A for this local single-player mod.

## P-SVE-01 — Guarded one-time detection with Vanilla fallback (Resilience)
- Detection + selection + seam construction run once at `GameLaunched`, wrapped in a try/guard.
- On any unexpected failure, log a warning and assign `VanillaExpansionProfile`. The mod never crashes or self-disables due to compat logic.
- Satisfies NFRU-01/02, NFR-SVE-01/04.

## P-SVE-02 — Cached singleton active profile (Performance)
- The selected `IExpansionProfile` and the constructed `ExpansionCompatService` are built once and held as singletons for the session.
- Runtime consumers call the cached seam; no `IModRegistry` queries or profile re-selection occur per tick/frame/tile.
- Satisfies NFRU-03/04, NFR-SVE-06.

## P-SVE-03 — Pure-Core decision seams + thin Mod adapter (Testability / Determinism)
- All decisions are pure functions in `Dayswork.Core/Compat` (`ExpansionProfileSelector`, `AnimalBuildingCapacityPolicy`, `IExpansionProfile` lookups) — no SMAPI/Stardew refs.
- The Mod adapter (`ExpansionCompatService`) only gathers live-object inputs and forwards to the pure layer.
- Enables xUnit + FsCheck without the game (NFRU-05/10, NFR-SVE-03/05).

## P-SVE-04 — Null-Object (no-op) Vanilla profile (Vanilla invariance)
- `VanillaExpansionProfile` is a Null-Object: all override lookups return "no override," so consumers run their existing code unchanged.
- This is the structural guarantee behind NFR-SVE-01 / S-21. In U-SVE-01 the SVE profile's tables are also empty, so SVE-active behavior is likewise unchanged until U-SVE-02..04.

## P-SVE-05 — Strategy + ordered registry selection (Extensibility)
- Profiles are interchangeable strategies; the selector scans a priority-ordered registry of detection predicates and returns the first match, else Vanilla.
- Adding an expansion = add a strategy + register it; no consumer changes (NFRU-09, NFR-SVE-02).

## P-SVE-06 — Clamp-not-throw capacity policy (Reliability / Determinism)
- `AnimalBuildingCapacityPolicy.DeriveCapacity` clamps to `[0, MaxOccupants]` and is total (never throws), deterministic for equal inputs (NFRU-02/05).

## Pattern → NFR map

| Pattern | NFRU | NFR-SVE |
|---|---|---|
| P-SVE-01 | NFRU-01/02 | NFR-SVE-01/04 |
| P-SVE-02 | NFRU-03/04 | NFR-SVE-06 |
| P-SVE-03 | NFRU-05/10 | NFR-SVE-03/05 |
| P-SVE-04 | NFRU-07 | NFR-SVE-01 |
| P-SVE-05 | NFRU-08/09 | NFR-SVE-02/07 |
| P-SVE-06 | NFRU-02/05 | NFR-SVE-04 |

## Extension Compliance

| Extension | Status | NFR-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security patterns required. |
| Property-Based Testing | Enabled, full | Compliant — P-SVE-03/06 keep the hard logic pure for FsCheck (selection, capacity, no-op profile). |
