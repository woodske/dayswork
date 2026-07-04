# Plan — Multi-target stand coverage ("Option D")

**Status:** proposed 2026-07-03, **deferred — not started.** Prerequisite: the managed-crop
serpentine ordering fix ("Option A", planned separately) must ship and smoke-pass first; re-measure
afterwards whether this is still worth building. This document records the design discussion so the
idea isn't re-derived later.

## Problem

Today every tile action costs one walk: the worker navigates to a stand tile, performs one beat,
then navigates to the next tile's stand. For dense full-coverage work — watering a whole field is
the canonical case — that means visiting essentially every target tile even though the worker's
reach could serve many targets from one standing spot. On a 9×14 managed field that is ~126
walk-stops for watering alone. The serpentine ordering fix (Option A) makes those stops monotonic
and cheap, but the *number* of stops is untouched.

## The reach model (verified in code)

- The worker may act on the tile it stands on **or any of the 8 surrounding tiles** — player
  parity, per the comment and enumeration in
  `Dayswork/Orchestration/WorkAreaScanner.cs` (`AdjacentInteractionTiles`, cardinals listed first,
  diagonals tagged `Diagonal: true`).
- For tasks that don't require adjacent navigation (watering/crop harvest among them,
  see `WorkAreaScanner.RequiresAdjacentNavigation`), `FindNavigationTiles` offers the target tile
  itself as the stand — and the managed-crop path (`ResolveManagedNavTile` in
  `ShiftOrchestrator.ManagedCrops.cs`) already stands *on* the target when passable. Self-tile
  watering is exercised in-game today.
- Trellis crops are the exception: `IsTrellisCrop` forces adjacent stands in the generic path, and
  a planted trellis tile is impassable — the coverage planner must never choose a trellis tile as a
  stand.

Therefore one stand tile covers up to **9 targets (its 3×3 neighborhood)**.

## The idea

Replace per-tile visits with **stand visits**: group the planned tile actions by chosen stand
tiles so the worker walks to one spot and performs several beats (water left, water front, water
right, …) before moving on.

On a dense rectangle the optimal structure is banding: walk every third row, stopping every third
column. A 9-wide row band of 3 rows needs 3 stands; a 9×14 field is 5 bands ≈ **15 stands instead
of ~126** — roughly 8× fewer walk-stops for a full watering pass. Real fields (sprinkler posts,
trellis rows, scarecrows, partial watering) will do worse than the ideal, but the win stays large
whenever the work is dense.

Formally this is a set-cover problem (NP-hard in general), but the domain — axis-aligned crop
fields with holes — doesn't need an optimal solver:

1. **Banding heuristic (preferred):** partition target rows into bands of 3; within a band, sweep
   columns and greedily place a stand every 3 columns where ≥1 target exists in the band; order the
   stands serpentine (bands top→bottom, alternating direction). Simple, deterministic, near-optimal
   on rectangles, degrades gracefully to per-tile visits on sparse work (a stand covering one
   target is just today's behavior).
2. Greedy set cover (pick the stand covering the most uncovered targets, repeat) is the fallback if
   banding proves too rigid around obstacles — better coverage, worse path coherence, needs an
   ordering pass afterwards.

## Design sketch

**Core (`Dayswork.Core`, pure, unit-tested):** a `StandCoveragePlanner` that consumes the
serpentine-ordered per-location action list produced by the Option A merge and emits an ordered
list of `StandVisit(standTile, actions[])`. Inputs must include a **passability/stand-eligibility
snapshot** (grid or predicate captured in `Dayswork/`, passed in — Core purity, hard rule 1) so the
planner never picks an impassable, out-of-zone, or trellis stand. Per-tile action *chains* stay
intact: a tile's clear→till→fertilize→seed→water sequence must execute in order within (or across)
visits.

**Execution (`Dayswork`):** the managed dispatch loop gains a stand-visit intent carrying multiple
actions. Per action within a visit: re-check `IsManagedActionApplicable`, face the target
(`FacingToward` already handles diagonal targets), play the tool swing, run the beat through
`RunGuardedWorkerBeat` (sound-location redirect + leak sweep, unchanged — see
`docs/sound-cues.md`), spend stamina. The visit advances to its next action when the swing
completes; the existing tick/`ActionPending` machinery generalizes from "one action then move" to
"next action in visit, else move".

**Exclusions from grouping (keep per-tile):**
- `ClearDebris` — multi-hit retry-in-place (tree→stump→gone) and tool-capability gating make it a
  poor grouping candidate; leave it on the per-tile path.
- Anything on a tile whose applicability flips mid-visit simply skips (same guard as today).

**What must NOT change:**
- **Energy/pricing:** stamina is charged per action beat, not per walk — grouping saves wall-clock
  time only (more work done before the 8 pm cap), never energy or money. No pricing surface moves.
- **Item routing:** harvest beats still route through the same buffer/provenance path (hard rule 4).
- **Sounds:** one cue per action, gated on the player's location, exactly as today.

## Scope

- **v1: `Water` only, managed-crop path only.** Watering is the densest task, has self+8 reach, no
  output, no supply, no multi-hit — the simplest applicability predicate and the biggest visible
  win.
- **v2 candidates:** `Fertilize`/`PlantSeed` (supply-consuming, needs the chest-supply decrement to
  survive reordering; planting trellis seeds from adjacent stands is actually *safer* than today's
  stand-on-tile), managed `Harvest` (output-carrying), then the generic task path (greenhouse
  watering/harvest phases) which already has stand-tile machinery (`StandTile`,
  `TrySelectPreferredStandTile`) to build on.

## Open questions

- Facing/animation polish: rapid re-facing between beats at one stand may look robotic; may want a
  minimum beat spacing or a small turn animation.
- Watering-can capacity is not modeled by the mod (`InvokeWater` sets dirt state directly, no
  refill mechanic); if a refill mechanic is ever added, coverage planning interacts with trip
  planning.
- Whether banding should align to sprinkler-free gaps automatically or stay purely geometric.

## Decision record

- 2026-07-03 — discussed as "Option D" alongside the managed-crop ordering fix. Agreed: ordering
  fix (Option A) ships first; coverage planning is deferred and re-evaluated against real shift
  timings afterwards. Zones stay: they are the layout/scoping/pricing model (sprinkler-aligned
  layouts, supply math via `PlannedPlantableTileCounter`, persistence) — the pathing problem was
  execution *order*, not the zone model.
