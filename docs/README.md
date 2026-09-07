# Dayswork docs

Four kinds of document, kept apart on purpose. **Facts → `game-data/`. Reasoning → `analysis/`.
Scheduled work → `plans/`. The current shape of the code → `architecture.md`.**

| Path | Holds | Index |
|---|---|---|
| [architecture.md](architecture.md) | How the mod is built today — the subsystem map and the shift loop. Start here for a code task. | — |
| [game-data/](game-data/index.md) | Verified Stardew/SVE content, confirmed against game files, a decompile, or observed runtime behavior (AGENTS.md hard rule 7). Facts, not decisions. | [index](game-data/index.md) |
| [analysis/](analysis/index.md) | Research, feasibility studies, and weighed design tradeoffs. Carries no status — the value is the reasoning. | [index](analysis/index.md) |
| [plans/](plans/index.md) | Implementation plans, with `archive/` for finished ones. Status lives *only* in its index. | [index](plans/index.md) |
| [prompts/](prompts/) | Prompts produced through AI collaboration for big features. Inputs to a session, not records of one — deliberately not indexed or tracked. | — |

Deciding where something new goes:

- Did you *confirm* it against the game? → `game-data/`, and add an index row.
- Is it the record of a decision, a measurement, or an option you rejected? → `analysis/`.
- Is it work you intend to schedule, with acceptance criteria? → `plans/`, with a status row in its
  index and a **Verify** command that detects the done-state from the code alone.
- Does it describe how the mod works right now? → `architecture.md`.

Only the leaf indexes describe individual files; this page routes and nothing more, so it stays
correct as long as the folders do.
