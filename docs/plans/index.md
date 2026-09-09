# Plan index & implementation state

Single tracking surface for everything in `docs/plans/`. **Status lives only in this file** — plan
files hold the detail (problem, design, decisions, acceptance criteria) but not status, so there is
one place to keep current and one place that can go stale.

Completed plans are summarised in [`archive/index.md`](archive/index.md) and their plan files
deleted. Archiving is done by the **`archive-plans` skill** — see the maintenance protocol below.

Research, feasibility studies, and weighed-but-unscheduled design records are **not** plans — they
live in [`../analysis/`](../analysis/index.md) and carry no status. Verified game content lives in
[`../game-data/`](../game-data/index.md).

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
| `DONE <date>` | All acceptance criteria met — archive it |
| `SUPERSEDED (→ plan)` | Made unnecessary by other work; note why |

## Active plans

*Last verified against code: 2026-09-08.*

| Plan | Status | Depends on | Verify (state of the code, not the doc) |
|---|---|---|---|
| [dayswork-2.0](dayswork-2.0.md) — N offices, ownership, multiplayer, appearance, painting (six phases) | `PARTIAL 2026-09-08` — **all six phases (0–5) built** on branch `dayswork-2.0`; review reran build + 786 passing tests (one intentional skip). **S1–S32 remain unrun**, and the [implementation review](dayswork-2.0-review-fixes.md) identified corrections required before release. Do not mark `DONE` or archive until the fixes and expanded smoke matrix pass | — | Phase 0: `grep -rni cabin Dayswork Dayswork.Tests` → only the footprint comment. Phase 1: `grep -rn "class ShiftFleet\|class OfficeContractStore" Dayswork/` hits and `grep -rn OnePerFarmBuildCondition Dayswork/` is empty. Phase 2: `grep -rn "Game1\.player\.Money" Dayswork/` is empty. Phase 3: `ls Dayswork/assets/farmhand_PaintMask.png` and `grep -rn "class WorkerAppearances" Dayswork.Core/` both hit. Phase 4: `grep -rn "class MultiplayerGuard" Dayswork/` is empty and `grep -rn "PeerContextReceived" Dayswork/` hits; `ls Dayswork.Core/Net Dayswork/Net` both list files. Phase 5: `grep -rn "Data/PaintData" Dayswork/` hits. |
| [dayswork-2.0-review-fixes](dayswork-2.0-review-fixes.md) — 11 review findings: lifecycle, item safety, compatibility, owner scheduling, and client state | `IN PROGRESS 2026-09-08` — R1–R9 implementation checkpoint complete: lifecycle isolation, shopping settlement, compatibility gating/cleanup, offline scheduling, exact tree-drop attribution, authoritative contract state on every answer, remote upgrade state on every entry path, and host-quoted contract terms with a requote before any charge. Focused review suite: 77 passing; full suite: 863 passing, one intentional skip. **Protocol version is now 4** (R7 and R9 each added message fields). R3/R4/R6/R7/R9 still have explicit in-game/transport acceptance rows open; R10 is next | Built phases of dayswork-2.0 | `rg -n Dayswork2Review Dayswork.Tests` must discover regressions for R1–R11; `dotnet test Dayswork.Tests/Dayswork.Tests.csproj -p:EnableModDeploy=false --filter FullyQualifiedName~Dayswork2Review` must run them, not return a zero-test pass. Then run the full suite and expanded S1–S32 smoke matrix. |

### Notes

**dayswork-2.0** supersedes the *recommendation* of
[../analysis/multiplayer-readiness.md](../analysis/multiplayer-readiness.md) (its facts still hold).
It also revives, on their merits, three pieces deleted by the 2026-08-30 revert (`f4116b5`):
`WorkClaimRegistry`, `ShoppingBudgetLedger`, and the per-contract `FarmhandNpc.Name` +
`getTextureName()` override. The many-workers-per-office model is **not** coming back.

**time-aware-wrapup** was parked and archived on 2026-09-07 without being completed — see
[archive/time-aware-wrapup.md](archive/time-aware-wrapup.md). Its Phase-0 code (`ShiftClockEstimator`,
its tests, and the DevLog-gated `MeasureWrapUpFit`) is **still live in the tree and
behavior-neutral**; it is the resume point, not dead code. Pull the plan back out of the archive if
the work is picked up again.

Two further entries moved to [`../analysis/`](../analysis/index.md) — neither is scheduled work, so
neither carries a status any more:

- **[stand-coverage-routing](../analysis/stand-coverage-routing.md)** — a design record kept so the
  idea isn't re-derived. Its gate is a measurement, not a dependency; the serpentine ordering fix it
  was to be measured against shipped 2026-07-03 and passed its smoke pass 2026-07-07. Verify with
  `grep -rn "StandCoveragePlanner" Dayswork.Core/` — no hits means not started.
- **[multiplayer-readiness](../analysis/multiplayer-readiness.md)** — a feasibility study, not a
  plan. Its *recommendation* was superseded by dayswork-2.0, whose Phase 4 (built 2026-09-07)
  implements the host-authoritative model it sketched. Its facts still hold, but it describes the
  1.x codebase, so read it as history. **Do not advertise the mod as multiplayer-ready until
  S10–S31 have actually been run in-game** — Phase 4 is built and unit-tested, not smoke-tested.

## Recently archived

Full write-ups — why, and what was decided — are in the archive. Everything below **shipped**,
except the parked entry, which is called out as such.

| Work | Landed | Archived in |
|---|---|---|
| Architecture review items #0–#7 (pathfinding/passability cache, batch ordering, work-activity list, deposit ordering, pathing polish) | 2026-07-07 | [archive/architecture-review-2026-07-07.md](archive/architecture-review-2026-07-07.md) |
| Manage Machines (+ auto-grabber and cross-location input chests) | 2026-06-19 → 2026-07-06 | [archive/machine-and-pond-management.md](archive/machine-and-pond-management.md) |
| Manage Fish Ponds, and the flavored/quality preservation pipeline | 2026-06-23 | [archive/machine-and-pond-management.md](archive/machine-and-pond-management.md) |
| Time-aware wrap-up (#5) — **parked, not completed**; only the measure-only Phase 0 shipped | Phase 0: 2026-07-07; parked 2026-09-07 | [archive/time-aware-wrapup.md](archive/time-aware-wrapup.md) |

Work shipped without its own plan file (tracked in `AGENTS.md` → "Current state"): serpentine sweep
routing (2026-07-03).

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
6. Add a row with a **Verify** command that detects its done-state from the code alone. If the
   document is research or a tradeoff record rather than schedulable work, it belongs in
   [`../analysis/`](../analysis/index.md) instead — index it there and give it no status.

**When archiving:**
7. Run the **`archive-plans` skill**. It folds `DONE` plans into an artifact under `archive/`,
   updates both indexes, repoints cross-references, and deletes the plan file. Do not archive by
   hand, and do not archive a plan whose headline deliverable has not shipped **unless the user
   explicitly parks it** — a parked plan's artifact must say so at the top, keep the design sketch
   (there is no code to serve as the record), and state what is still live in the tree.
