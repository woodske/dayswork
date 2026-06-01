# Functional Design Plan — U-SVE-02 SVE Farm Maps + Worker Entrance

**Stage**: CONSTRUCTION → U-SVE-02 → Functional Design (Part 1: plan + questions). Artifacts generated after answers resolve.

**Unit**: U-SVE-02 (see [unit-of-work.md](../../inception/application-design/unit-of-work.md)). **Story**: S-22. **Requirements**: FR-SVE-05/06/15. Depends on U-SVE-01 (the seam) — now built and approved.

## Grounded facts (verified in source)

- **Entrance logic today**: `ShiftOrchestrator.FindFarmExitTile(Farm)` is a `private static` method that scans `farm.warps` for the first outdoor exit warp, clamps/searches for a passable approach tile, and falls back to `(77,15)`. It already accesses `ModEntry`-level statics, so it can consult `ModEntry.ExpansionCompat`.
- **SVE farms replace `Maps/Farm`**: the IF2R Content Patcher pack edits `Maps/Farm` (and patches `Maps/BusStop` / woods warps); the `GameLocation` remains named `"Farm"`. The same pattern applies to Grandpa's Farm and Frontier Farm. **Therefore the active farm cannot be identified by location name** — all three present as `"Farm"`.
- **Farm-map mods are separate and effectively mutually exclusive**: each replaces `Maps/Farm`; SVE instructs players to install only the one they want. Their mod ids are already known to the seam (`flashshifter.immersivefarm2remastered`, `flashshifter.GrandpasFarm`, `flashshifter.FrontierFarm`).
- **Seam readiness**: `IExpansionProfile.TryGetEntranceOverride(string farmIdentity, out TileCoord)` is already generic — `farmIdentity` can be the active farm-map id rather than the location name. No interface change is forced.

## Design focus

1. **Farm-map identity** — how to know which SVE farm is active despite the shared `"Farm"` location.
2. **Entrance resolution rule** — how the per-map override and the existing `farm.warps` heuristic combine.
3. **Integration** — wiring `FindFarmExitTile` (and the morning spawn + shift-exit paths that use it) to consult the seam first.
4. **Graceful skip** — unreachable tiles on SVE maps continue to be skipped as today (FR-SVE-15).

## Questions

## Question 1 — How is the active SVE farm map identified?
A) **(Recommended)** By the **installed SVE farm-map mod id** (IF2R / Grandpa's Farm / Frontier), resolved at detection via `ModRegistry`. These packs are mutually exclusive in practice, so the installed farm-map id reliably names the active farm. If (unsupported) more than one is installed, log a warning and fall back to the warp heuristic.
B) By a **map signature** (farm map width/height and/or a unique tile or map property), robust to multiple farm-map mods being installed, but more complex and itself requires verified per-map signatures.
X) Other (please describe after [Answer]: tag below)

[Answer]: B, I believe they can be loaded at the same time

## Question 2 — Entrance resolution rule
A) **(Recommended)** Consult the seam's **per-map entrance override first**; if there is no override for the active farm, use the existing `farm.warps` heuristic + `(77,15)` fallback unchanged (FR-SVE-06). A map only gets an override if its heuristic result is wrong in playtest; maps where the heuristic already works get no override.
B) For SVE farms, **always** use an explicit per-map entrance tile and bypass the heuristic entirely.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — How override tile values are obtained (process confirmation)
A) **(Recommended)** Determine each supported map's entrance tile from the **SVE map source** (the BusStop→Farm arrival / farm entry tile) and **confirm via manual SVE playtest**; encode only verified tiles. No assumed coordinates (NFR-SVE-03).
B) Encode best-guess entrances now from the maps and refine later if playtest shows problems.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (artifact generation — after answers resolved)

- [x] Create `construction/u-sve-02-farm-maps-entrance/functional-design/business-logic-model.md` (farm-map identity resolution, entrance-resolution flow combining override + heuristic, spawn/exit integration points).
- [x] Create `.../functional-design/business-rules.md` (BR-SVE2-.. incl. identity rule, override-first-then-heuristic, multiple-farm-map fallback, unreachable-skip, no-assumed-coords).
- [x] Create `.../functional-design/domain-entities.md` (farm-map identity value; entrance-override table shape; note no persistence change).
- [ ] Extension compliance (Security N/A; PBT full — entrance-resolution selection is pure/testable).
- [ ] Update `aidlc-state.md` and append to `audit.md`.
