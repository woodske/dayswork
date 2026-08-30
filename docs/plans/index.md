# Plan index & implementation state

Single tracking surface for everything in `docs/plans/`. **Status lives only in this file** — plan
files hold the detail (problem, design, decisions, acceptance criteria) but not status, so there is
one place to keep current and one place that can go stale.

Completed plans are summarised in [`archive/index.md`](archive/index.md) and their plan files
deleted. Archiving is done by the **`archive-plans` skill** — see the maintenance protocol below.

> **Never trust a plan doc's claims about the codebase.** Run the **Verify** command in the table
> below before starting or resuming any plan. The code is ground truth; this index is a map. The
> 2026-07-07 review learned this the hard way — item #0's premise ("no tick throttle exists") was
> itself wrong, and the doc turned out to be right.

## Status legend

| Status | Meaning |
|---|---|
| `NOT STARTED` | No implementation work done |
| `DESIGN PENDING` | Needs a decision/design session with the user before implementation |
| `IN PROGRESS <date>` | Actively being implemented; note which phase |
| `PARTIAL <date>` | Some phases shipped, the headline deliverable has not |
| `BLOCKED (→ plan)` | Waiting on another plan or a decision |
| `DEFERRED <date>` | Designed and consciously not scheduled; the condition to revisit is in the plan |
| `ANALYSIS` | Research/decision record, not itself implementable |
| `DONE <date>` | All acceptance criteria met — archive it |
| `SUPERSEDED (→ plan)` | Made unnecessary by other work; note why |

## Active plans

*Last verified against code: 2026-08-30.*

| Plan | Status | Depends on | Verify (state of the code, not the doc) |
|---|---|---|---|
| [time-aware-wrapup.md](time-aware-wrapup.md) | `PARTIAL 2026-07-07` — Phase 0 (measure-only) shipped; the live skip-gate is **not** enabled, awaiting headroom calibration from real play | — | `grep -rn "MeasureWrapUpFit" Dayswork/Orchestration/` hits (Phase 0 present). The gate is live when `DayEndingSoon` is passed to `QueueWrapUpNow` — not just declared on `ShiftStopReason` |
| [stand-coverage-routing.md](stand-coverage-routing.md) | `DEFERRED 2026-07-03` — prerequisite met 2026-07-07; awaiting a re-measure against real shift timings | passability grid (shipped) | `grep -rn "StandCoveragePlanner" Dayswork.Core/` — no hits means not started |
| [multiplayer-readiness.md](multiplayer-readiness.md) | `ANALYSIS` — Dayswork is single-player by design; this is the effort estimate and support model, not scheduled work | — | `Dayswork/Guards/MultiplayerGuard.cs` still returns a blanket `Context.IsMultiplayer`, and no host-authority layer exists |

### Notes on the active three

- **time-aware-wrapup** is the only one with code in flight. What remains is not a build task first:
  collect a play-day or two of `[Dayswork][wrapup-measure]` lines, confirm `would-skip` fires only
  for genuinely-doomed late trips (not at 6pm), calibrate `WrapUpWorkHeadroomMinutes` (provisional
  10) from that data, *then* replace the measurement log with the real gate.
- **stand-coverage-routing** is a design record kept so the idea isn't re-derived. Its gate is a
  measurement, not a dependency — the serpentine ordering fix it was to be measured against shipped
  2026-07-03 and passed its smoke pass 2026-07-07.
- **multiplayer-readiness** stays here rather than in `docs/` because it describes work that could be
  scheduled; nothing in it has been implemented. Do not advertise the mod as multiplayer-ready.

## Recently completed

Full write-ups — why, and what was decided — are in the archive.

| Work | Landed | Archived in |
|---|---|---|
| Architecture review items #0–#7 (pathfinding/passability cache, batch ordering, work-activity list, deposit ordering, pathing polish) | 2026-07-07 | [archive/architecture-review-2026-07-07.md](archive/architecture-review-2026-07-07.md) |
| Manage Machines (+ auto-grabber and cross-location input chests) | 2026-06-19 → 2026-07-06 | [archive/machine-and-pond-management.md](archive/machine-and-pond-management.md) |
| Manage Fish Ponds, and the flavored/quality preservation pipeline | 2026-06-23 | [archive/machine-and-pond-management.md](archive/machine-and-pond-management.md) |

Work shipped without its own plan file (tracked in `AGENTS.md` → "Current state"): serpentine sweep
routing (2026-07-03) and multiple farmhands (2026-07-08).

## Maintenance protocol

**Before starting a plan:**
1. Run its **Verify** command. If the code already satisfies parts of the plan, update the plan file
   to remove finished work *before* implementing.
2. Set status to `IN PROGRESS <date>` here, noting the phase if multi-phase.

**While implementing:**
3. Tick acceptance criteria **in the plan file** — that checklist is the fine-grained record; this
   index holds only coarse status.
4. If scope changes mid-flight (a phase dropped, an approach swapped), edit the plan file to match
   what is actually being built. A plan that disagrees with its implementation is worse than no plan.
   Record *why* it changed — that reasoning is what the archive preserves.

**When finishing:**
5. Verify every acceptance criterion, set status to `DONE <date>` here, and update the *Last
   verified* date.

**When adding a new plan:**
6. Add a row with a **Verify** command that detects its done-state from the code alone.

**When archiving:**
7. Run the **`archive-plans` skill**. It folds `DONE` plans into an artifact under `archive/`,
   updates both indexes, repoints cross-references, and deletes the plan file. Do not archive by
   hand, and do not archive a plan whose headline deliverable has not shipped.
