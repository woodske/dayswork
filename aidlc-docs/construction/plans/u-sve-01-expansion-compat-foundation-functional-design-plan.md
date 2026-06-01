# Functional Design Plan — U-SVE-01 Expansion-Compatibility Provider Foundation

**Stage**: CONSTRUCTION → U-SVE-01 → Functional Design (Part 1: plan + questions). Artifact generation runs after answers are resolved.

**How to use this file**: answer each `[Answer]:` tag with a letter (or `X` + description). Reply "done" when finished.

**Unit**: U-SVE-01 — provider foundation + detection (see [unit-of-work.md](../../inception/application-design/unit-of-work.md)). **Stories**: S-21 (vanilla invariance + SVE auto-detect), S-26 (provider seam / extensibility / PBT). **Components**: C-19..C-23, M-22, M-23 ([sve-compatibility-application-design.md](../../inception/application-design/sve-compatibility-application-design.md)).

**Scope reminder (Q3 of unit plan = A)**: U-SVE-01 ships the *complete seam* with the Vanilla profile fully working and the SVE profile present but with its override tables **empty/stubbed** (entrance, content, work-location, premium-tier tables are populated by U-SVE-02..04). So this unit's functional design is about the **seam's pure logic and contracts**, not concrete SVE data values.

---

## Functional design focus for this unit

1. **Profile selection** — deterministic choice of active profile from installed mod IDs (C-20).
2. **Vanilla fall-through** — every Vanilla-profile lookup is a documented no-op so consumers keep existing behavior (NFR-SVE-01 / S-21).
3. **Capacity policy** — pure derivation algorithm replacing the hardcoded ladder (C-23), and how it stays vanilla-safe.
4. **Detection lifecycle** — when detection runs, caching, and logging.
5. **Contracts** — the lookup/operation signatures the seam exposes (already sketched in App Design; FD pins the rules/semantics).

---

## Questions

## Question 1 — Animal-building feed-capacity derivation rule (vanilla-safety sensitive)
`C-23 AnimalBuildingCapacityPolicy` replaces the hardcoded `Deluxe=12 / Big=8 / else=4` ladder in `AnimalTaskHandler.FeedCapacity`. Note: the actual feeding loop already places hay on real empty **"Trough"** Back-layer tiles via `ResolveEmptyTroughTiles`; the old ladder was only a clamp/short-circuit.

A) **(Recommended)** Capacity = **count of real "Trough" tiles** in the `AnimalHouse`, clamped to the building's `MaxOccupants`. This matches what the feeding loop actually does, and is correct for vanilla and premium by construction. **Vanilla parity is verified** against vanilla coop/barn maps during Code Generation; if a vanilla tier's real trough count differs from the old ladder number, the trough-true value wins (the ladder was an approximation, and feeding is already per-trough).
B) Capacity = building-data **`MaxOccupants`** (16 premium; 4/8/12 vanilla), independent of trough tiles.
C) **Most conservative**: keep the exact old ladder for the three known vanilla tiers, and use trough/occupant derivation **only** for premium/unknown buildings. Maximizes provable vanilla invariance at the cost of keeping the legacy ladder around.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Profile selection precedence (selector determinism + future expansions)
A) **(Recommended)** Selection is a deterministic, priority-ordered scan over the known expansion profiles' detection predicates; return the first whose required mod IDs are present, else `VanillaExpansionProfile`. With Vanilla + SVE today, SVE is chosen when its content ID (`FlashShifter.StardewValleyExpandedCP`) is present. The precedence rule is documented so additional profiles slot in cleanly later.
B) Assume at most one expansion is present; if multiple ever match, log a warning and fall back to Vanilla.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Detection lifecycle, caching & logging
A) **(Recommended)** Detect **once at startup** (after all mods are loaded — SMAPI `GameLaunched`), cache the active profile for the session, and log it once at debug. Vanilla-profile seam operations are pure passthrough/no-op. No re-evaluation per save.
B) Detect lazily on first use, or re-evaluate on each save load.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (artifact generation — after answers resolved)

- [x] Create `construction/u-sve-01-expansion-compat-foundation/functional-design/business-logic-model.md` (selection flow, vanilla fall-through, capacity derivation, detection lifecycle).
- [x] Create `.../functional-design/business-rules.md` (BR-SVE-01.. rules incl. vanilla-invariance, determinism, capacity, logging; testable-property table for PBT).
- [x] Create `.../functional-design/domain-entities.md` (pure types: `IExpansionProfile`, `ContentDescriptor`, `WorkClassification`, `AnimalBuildingCapacityInputs`, selection inputs/outputs; note no persistence changes).
- [x] Extension compliance tables (Security N/A; PBT full — selection + capacity properties).
- [x] Update `aidlc-state.md` and append to `audit.md`.
