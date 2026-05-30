# U-SVE-03 — Business Rules — SVE Animal Buildings

**Unit**: U-SVE-03 · **Story**: S-23 · **Decisions**: Q1=A, Q2=A, Q3=A, Q4=A

## Business rules

### Feeding / capacity
- **BR-SVE3-01** — An animal building that auto-feeds (vanilla Deluxe, or any building whose interior map sets `AutoFeed = T`, which includes SVE Premium Coop/Barn) produces **no manual feed work**. This gate is unchanged from vanilla behavior.
- **BR-SVE3-02** — For a building that is *not* auto-feeding, feed capacity = `min(troughTileCount, maxOccupants)`, where `troughTileCount` is the number of `Trough` map-property tiles in the interior and `maxOccupants` is the building's real max occupants. This replaces the hardcoded `Deluxe=12 / Big=8 / else=4` ladder.
- **BR-SVE3-03** — Capacity derivation is total and deterministic: negative inputs are treated as zero; it never throws (carried from `AnimalBuildingCapacityPolicy`, pattern P-SVE-06).
- **BR-SVE3-04** — For every vanilla animal building, the data-driven capacity equals the value the old ladder produced (vanilla parity — asserted with concrete examples at code-gen). No vanilla feeding behavior changes.

### Scope / pricing tier
- **BR-SVE3-05** — `SveExpansionProfile.MapPremiumBuildingTier` maps `FlashShifter.StardewValleyExpandedCP_PremiumCoop → DeluxeCoop` and `FlashShifter.StardewValleyExpandedCP_PremiumBarn → DeluxeBarn`; every other building type → `null`.
- **BR-SVE3-06** — During hiring enumeration, the premium tier mapping (via `ExpansionCompatService.ResolveAnimalBuildingTier`, keyed on the raw `buildingType`) is consulted **before** the vanilla name-substring inference and wins when it returns a tier. When it returns `null`, the existing vanilla inference runs unchanged.
- **BR-SVE3-07** — No new `AnimalBuildingTier` enum value and no save-schema change are introduced; premium buildings persist and price as their mapped vanilla Deluxe tier (App Design Q4=A).
- **BR-SVE3-08** — All SVE building identifiers live exclusively in `SveExpansionProfile` (NFR-SVE-07); no SVE string literals leak into `LegacyScopeBootstrapper`, `AnimalTaskHandler`, or other consumers.

### Pet / Collect (no special-casing)
- **BR-SVE3-09** — Pet and Collect work by scanning animals (`wasPet`, `currentProduce`) and naturally find nothing to do when an auto-petter/auto-grabber already acted. Dayswork makes no machine-presence assumption and does not special-case `(BC)272`/`(BC)165` (requirement Q4=A refinement).

## Property-based test (PBT) table — FsCheck (full mode, blocking)

| Property | Statement |
|---|---|
| **P1 — capacity clamp** | For all `(troughs, max)`, `DeriveCapacity = clamp(troughs, 0, max)` and result ∈ `[0, max]`; never throws (incl. negatives). |
| **P2 — capacity monotonic in troughs** | For fixed `max`, capacity is non-decreasing as `troughs` increases (up to the `max` clamp). |
| **P3 — vanilla parity** | For each vanilla tier's representative `(troughs, max)`, data-driven capacity equals the legacy ladder bucket (example-based, but stated as an invariant per tier). |
| **P4 — tier-map totality/determinism** | `MapPremiumBuildingTier` returns the same result for the same input every call, and returns non-null **only** for the two premium type strings. |
| **P5 — vanilla pass-through** | For any building type that is not a premium id, `ResolveAnimalBuildingTier(building, vanillaTier)` returns `vanillaTier` unchanged (identity). |
| **P6 — auto-feed skip invariance** | For any building with the auto-feed gate true, feed work is empty regardless of trough/occupant counts. |

## Traceability
- S-23 (premium barn/coop service) → BR-SVE3-01..09.
- NFR-SVE-07 (single home for SVE ids) → BR-SVE3-08.
- App Design Q4=A (no save/enum change) → BR-SVE3-07.
- Requirements Q4=A (no auto-machine special-casing) → BR-SVE3-09.
