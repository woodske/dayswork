# Business Rules — U-SVE-01 Expansion-Compatibility Provider Foundation

## Detection & selection

### BR-SVE-01 Deterministic profile selection
Selection is a pure, priority-ordered scan over the known expansion profiles' detection predicates. The first profile whose required mod IDs are all present wins; if none match, `VanillaExpansionProfile` is selected. The result is a deterministic function of the installed-mod-id set.

### BR-SVE-02 SVE detection identity
The SVE profile's detection predicate matches when the SVE content pack `FlashShifter.StardewValleyExpandedCP` is loaded; `FlashShifter.SVECode` is also recognized. (Farm-map IDs are recorded for U-SVE-02 use but are not required for SVE detection itself.)

### BR-SVE-03 Detect once, cache for session
Detection runs a single time at SMAPI `GameLaunched` (after all mods have loaded). The active profile is cached for the session and is not re-evaluated on save load or per tick.

### BR-SVE-04 Log active profile once
The active profile id is logged exactly once at `Debug` level at startup for maintainer diagnosis (S-21). No per-operation logging.

## Vanilla invariance

### BR-SVE-05 Vanilla profile is a pure no-op
For `VanillaExpansionProfile`, every override lookup returns "no override": `TryGetEntranceOverride` → false, `TryClassifyContentOverride` → false, `IsExpansionWorkLocation` → false, `MapPremiumBuildingTier` → null (so `ResolveAnimalBuildingTier` returns the passed-in vanilla tier).

### BR-SVE-06 No-expansion behavior is identical to the prior release
With no recognized expansion installed, every consumer takes its existing code path. The only deliberate general change is `ResolveAnimalFeedCapacity` (now trough-derived), constrained by BR-SVE-09.

### BR-SVE-07 SVE override tables are empty in this unit
In U-SVE-01, `SveExpansionProfile` carries identity + farm-map IDs only; its entrance, content, work-location, and premium-tier tables are empty. Consequently, even with SVE active, this unit produces no override behavior. Override behavior is introduced by U-SVE-02..04.

### BR-SVE-13 Seam is the sole SVE-awareness point
No vanilla or `Dayswork.Core` call site contains SVE-specific branching or magic strings. SVE awareness exists only inside the compat seam (NFR-SVE-01/02/07).

## Capacity policy

### BR-SVE-08 Capacity = clamped trough count
`AnimalBuildingCapacityPolicy.DeriveCapacity(inputs)` returns `clamp(TroughTileCount, 0, MaxOccupants)`. `TroughTileCount` is the number of real "Trough" Back-layer tiles in the `AnimalHouse`; `MaxOccupants` comes from building data.

### BR-SVE-09 Vanilla parity is verified, not assumed
During Code Generation, the derived capacity for each vanilla animal-house tier is checked against the building's real trough tiles. The trough-true value is authoritative; the legacy `4/8/12` ladder is treated as an approximation. If verification reveals any vanilla regression risk, it is resolved before merge (escalate to the conservative per-tier path if needed).

### BR-SVE-10 Capacity derivation is pure and deterministic
`DeriveCapacity` depends only on its inputs, has no side effects, and is total (never throws) for non-negative inputs; negative or zero inputs clamp to 0.

## Boundaries

### BR-SVE-11 No persistence change
The active profile is derived at runtime. No save-data schema, DTO, or `config.json` change is introduced by this unit (premium-tier mapping in U-SVE-03 also avoids save changes per Q4=A).

### BR-SVE-12 No new player-facing strings in this unit
The only new log line (active profile) is a maintainer/debug diagnostic, exempt from i18n per the project's technical/debug exemption.

## Testable properties (PBT — FsCheck; full mode)

| Rule | Property category | Property |
|---|---|---|
| BR-SVE-01 | Invariant | For any installed-id set, exactly one profile is selected, deterministically; same input → same profile. |
| BR-SVE-01 | Invariant | When no recognized expansion id is present, the selected profile is `VanillaExpansionProfile`. |
| BR-SVE-01/02 | Invariant | When the SVE content id is present, the SVE profile is selected (regardless of other ids / order). |
| BR-SVE-05 | Invariant | All `VanillaExpansionProfile` override lookups return "no override" for every input. |
| BR-SVE-08 | Invariant | `DeriveCapacity` result is in `[0, MaxOccupants]` and equals `min(max(TroughTileCount,0), MaxOccupants)`. |
| BR-SVE-10 | Invariant | `DeriveCapacity` never throws for any integer inputs; deterministic for equal inputs. |

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant — selection determinism/precedence and capacity-clamp invariants are PBT-01 properties carried into Code Generation; vanilla no-op is example + property tested. |
