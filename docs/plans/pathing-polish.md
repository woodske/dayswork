# Plan — Pathing polish (gates, sweep segments, string-pulling)

**Status:** proposed 2026-07-07, not started. Item #7 in
[architecture-review-index.md](architecture-review-index.md). Three independent sub-items;
(a) has no dependencies and fixes a small real behavior bug, (b) and (c) need the passability
grid from [core-pathfinding-and-passability-cache.md](core-pathfinding-and-passability-cache.md).

## (a) Gates: open on approach, never leak open

**Problem.** `WorkerMovementDriver.OpenGatesAlongRoute` toggles **every** gate on the route the
moment the path is planned — a gate 40 tiles ahead visibly opens before the worker is anywhere
near it. Worse, `TryCloseGate` only fires for waypoints actually *reached*: any route cleared
mid-walk (work re-selection, stuck recovery, travel cancel, `Clear()`) leaves already-opened
gates open. Per `docs/fences-and-gates.md`, the auto-close rule runs in
`updateWhenCurrentLocation` only — so an off-screen leaked gate **stays open indefinitely**.

**Design.**
- Stop bulk-opening at plan time. Track the route's gate tiles (already identifiable during
  route construction); open a gate when it becomes the **next waypoint** (or the worker is within
  one tile), matching how a player reads the animation.
- Track opened-but-not-yet-closed gate tiles on the driver as route state. On `Clear()` /
  navigation completion / `WarpWorker`, close every tracked gate except one the worker is
  currently standing on (closing a gate onto the worker would trap or clip it — verify
  `toggleGate` behavior with an occupant against the decompile before relying on this, hard
  rule 7).
- Keep the existing close-behind on waypoint pass.

**Testing.** In-game: fenced pasture with 2+ gates; interrupt mid-route (cancel shift, trigger
stuck recovery) and confirm no gate is left open, on-screen and off-screen.

## (b) Serpentine sweep over reachable row segments

**Problem.** `SerpentineSweep.Rank` (Core/Geometry) is pure tile geometry: a row bisected by a
pond, fence line, or building forces the worker to detour around the obstacle and come back for
the row's far half — repeatedly, once per affected row. The full fix (stand-coverage "Option D")
is deferred; this is the cheap intermediate.

**Design.** Give `Rank` an optional passability predicate (`Func<TileCoord,bool>`, fed from the
cached grid — Core purity preserved via the predicate seam, same pattern as
[stand-coverage-routing.md](stand-coverage-routing.md) specifies). Split each occupied row into
**contiguous-reachable segments** (a segment breaks where the tiles between two work tiles are
impassable), then serpentine over *segments*: all of segment A's tiles, then the nearest end of
the next segment, alternating direction as today. No predicate ⇒ exactly today's output
(regression-safe default).

**Testing.** Core unit tests: bisected row produces two segments visited consecutively per side,
not interleaved; no-predicate parity with current output; orientation-pick (`PickOrientation`)
unchanged. In-game: field split by a pond, watch one watering pass.

## (c) String-pulling (waypoint smoothing)

**Problem.** Both `PathFindController` and the BFS fallback emit 4-directional paths; the driver
lerps tile-center to tile-center, so the worker walks visible staircases across open ground.
Purely cosmetic — vanilla NPCs are also 4-dir — hence lowest priority.

**Design.** Post-process the waypoint list in `WorkerMovementDriver` before enqueueing: greedily
drop intermediate waypoints while the straight segment between the survivors is clear, using a
tile **supercover** line test (every tile the segment touches, not just Bresenham's choices —
64px body must not clip corners) against the cached passability grid. The pixel-lerp movement
already handles arbitrary directions (`Vector2.Normalize` step + dominant-axis facing), so no
movement changes are needed. Constraint: never smooth *across a gate tile* — gates need the
waypoint touch for open/close bookkeeping from (a).

**Testing.** Core unit test for the supercover clearance test (extract it as a pure function);
in-game visual check across open fields and along fence corners (the classic corner-clip case).

## What must NOT change (all three)

- The walk-only invariant: no new warps.
- Route selector tie-break behavior — (b) changes intra-batch visit order by design, but only
  within the sweep's own ordering contract (SelectionKey = queue position), never across
  categories.
- Energy and item routing: all three items affect *where the feet go*, nothing else.

## Suggested order within this plan

(a) first — real bug, zero dependencies, small. Then (b) after the passability grid ships; (c)
last, and only if the staircase walking actually bothers anyone once (a)/(b) are in.
