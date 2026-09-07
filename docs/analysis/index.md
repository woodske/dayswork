# Analysis index

Research and decision records: investigations into how something works, feasibility studies, and
weighed design tradeoffs. A document belongs here when its value is the **reasoning** — the options
considered, the measurements taken, and why one path was chosen or deferred.

Boundaries with the neighbouring folders:

| Folder | Holds |
|---|---|
| `analysis/` (here) | Research, feasibility, design tradeoffs. Not scheduled work. |
| [`../plans/`](../plans/index.md) | Implementation plans with acceptance criteria + status; `archive/` holds finished ones. |
| [`../game-data/`](../game-data/index.md) | Verified Stardew/SVE content — facts, not decisions. |
| [`../architecture.md`](../architecture.md) | How the mod is built today (the subsystem map). |

Nothing here carries status. If an analysis turns into scheduled work, write a plan file in
`../plans/` and track its status in that index; leave the analysis in place as the record of why.

## Files

| File | What it covers |
|---|---|
| [multiplayer-readiness.md](multiplayer-readiness.md) | Feasibility study for multiplayer support — what the single-player assumptions are, the host-authority model it would need, and the effort estimate. Not scheduled; the mod is single-player by design. |
| [stand-coverage-routing.md](stand-coverage-routing.md) | Design record for multi-target stand coverage ("Option D") — planning one standing tile that services several work targets. Deferred 2026-07-03; its prerequisites shipped, so it is eligible for a re-measure against real shift timings. |

## Adding to this folder

1. One topic per file, named for the question it answers.
2. Open with **when** it was written and **what state of the code** it was written against — an
   analysis ages, and a reader needs to know how far to trust it.
3. Record the options that were rejected and why. That is the part that stops the discussion being
   re-run six months later.
4. Add a row above with a one-line summary that includes the outcome (adopted / deferred / declined),
   not just the topic.
