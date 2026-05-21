# U-13B — Tech Stack Decisions

**Unit**: U-13B — Farmer Worker + Tool Visuals

U-13B introduces **no new frameworks, NuGet packages, or external dependencies**. It builds entirely on the stack established in U-01/U-02 and the Stardew/SMAPI APIs already referenced by the `Dayswork` mod project.

---

## Confirmed (unchanged) stack

| Concern | Decision | Notes |
|---|---|---|
| Language / runtime | C# / .NET 6 | Per U-01; `Dayswork.Core` (no game refs) + `Dayswork` (mod) split unchanged. |
| Modding platform | SMAPI | `Display.RenderedWorld` event used for the worker render hook (FD-Q2=A). |
| Game APIs | `StardewValley.Farmer`, `FarmerRenderer`, `FarmerSprite`, `PathFindController` (path-compute-only), `Game1` viewport | All already available via the existing `Dayswork` references; no new assembly references. |
| Test framework | xUnit + FsCheck | Unchanged; `WorkerTool` map gets a table-driven xUnit test. |
| Harmony | No new patches | Rendering via SMAPI event, not a draw-pass patch (MAINT-U13B-03). |

---

## New types and where they live

| Type | Project | Stardew refs? | Test approach |
|---|---|---|---|
| `WorkerTool` (enum) + `ForTask` map | `Dayswork.Core` | No | Exhaustive table test (finite/total map) |
| `FarmhandWorker` (Farmer) | `Dayswork` | Yes | Play-test |
| `WorkerAppearance` (record) | `Dayswork` | Yes (`Color`) | Indirect (via randomizer) |
| `WorkerAppearanceRandomizer` | `Dayswork` | Yes | Light unit test (determinism: same `ContractId` → same appearance; indices in range) |
| `WorkerMovementDriver` | `Dayswork` | Yes | Play-test |
| `WorkerRenderer` | `Dayswork` | Yes | Play-test |
| `ToolSwapAnimator` (M-10) | `Dayswork` | Yes (`FarmerSprite`) | Play-test (frame sets) |

`WorkerMovementDriver` and `FarmhandWorker` replace (delete) `PathFindControllerAdapter` and `FarmhandNpc` respectively.

---

## Deferred engineering decision (resolve in NFR Design)

**TS-U13B-01 — Movement smoothness cadence.** The worker's `Position` can be advanced (a) every game tick (~60 Hz) for a perfectly smooth walk, (b) only on the throttled ~15 Hz work-logic sample (simplest, may look slightly choppy), or (c) on the 15 Hz sample with render-side interpolation between sampled positions (smooth draw, cheap logic). This is an internal pattern/quality decision, not a product preference, and is deferred to U-13B NFR Design with a final code-gen play-test confirm. *Default lean: (a) — step position every tick while keeping the heavier work/stuck/hit logic on the 15 Hz throttle, since position stepping is O(1) and the draw already runs every frame.*

---

## Rationale notes

- **No new dependencies** keeps the riskiest unit's surface area minimal — every moving part is either existing project code or a vanilla Stardew API the game already exercises every frame for the player.
- **`PathFindController` reused for path computation only** (FD-Q1=A) avoids re-implementing A* (a custom search would be more code and more risk) while sidestepping the `NPC.update()` controller-tick coupling that caused the U-13 "worker stands still" symptom.
- **`WorkerTool` is the only addition to `Dayswork.Core`**, preserving the Core-purity boundary (NFR-MAINT-03); everything game-coupled stays in `Dayswork`.
