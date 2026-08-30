# Plan archive

Summaries of implemented plans, grouped by theme. Each artifact records **why** the work was done and
what decisions and discussions the plan captured — not the step-by-step approach, which is now in the
code. The original plan documents were deleted after archiving.

Active and deferred plans live in [`../index.md`](../index.md).

| Artifact | What it covers |
|---|---|
| [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) | The 2026-07-07 pathing/efficiency review, items #0–#7 — the Core pathfinding extraction + passability cache, travel-aware batch ordering, the work-activity handler list, cross-location deposit ordering, and the gate/sweep-segment pathing fixes. |
| [machine-and-pond-management.md](machine-and-pond-management.md) | Manage Machines and Manage Fish Ponds — why ponds are a parallel subsystem rather than a machine special case, the physical-fetch input model, and the flavored/quality preservation work that came out of them. |

## Original plan → artifact

Docs, code comments, and commit messages may still reference these by filename.

| Original plan | Archived in |
|---|---|
| architecture-review-index.md | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| architecture-doc-refresh.md (#0) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| core-pathfinding-and-passability-cache.md (#2) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| travel-aware-batch-ordering.md (#3) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| work-activity-abstraction.md (#4) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| deposit-trip-ordering.md (#6) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| pathing-polish.md (#7) | [architecture-review-2026-07-07.md](architecture-review-2026-07-07.md) |
| machine-management.md | [machine-and-pond-management.md](machine-and-pond-management.md) |
| fish-ponds.md | [machine-and-pond-management.md](machine-and-pond-management.md) |

Review item **#5** was not archived — only its measure-only phase shipped, so it remains active as
[`../time-aware-wrapup.md`](../time-aware-wrapup.md). Item **#1** was a smoke-pass gate, not a plan
file.

## Open threads carried out of these plans

Deferred by decision, not blocked, and none scheduled. Recorded so they don't evaporate with the plan
files; the artifacts hold the reasoning.

- **Casks (cellar)** — the last machine-family phase. Verify the cask state model first.
- **Retrofit managed-crop seed consumption to physical fetch trips**, making crops symmetric with
  machines (which deliberately rejected the abstract-pool shortcut).
- **Passability cache phase 2** — targeted A* for sweep categories, gated on profiling.
- **Work-activity leftovers** — fold the `TravelPurpose` switch into per-activity continuations;
  move per-shift step state off `ShiftSession` into the activities.
- **String-pulling (#7c)** — waypoint smoothing; design stands, waiting on the staircase walking
  being observed to matter in play.
- **Batch-ordering leftovers** — expansion-location anchors, an "end the day near home" weight,
  cross-category chaining.

## Declined, so it isn't re-proposed

- **Up-front contract feasibility estimate** ("this scope likely exceeds one shift" at hire time) —
  reviewed 2026-07-07, declined by the user.
