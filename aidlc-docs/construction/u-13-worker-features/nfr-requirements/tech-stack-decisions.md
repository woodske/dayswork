# U-13 — Tech Stack Decisions

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability

---

## TS-U13-01 — No new frameworks or NuGet packages
Testing stays on **xUnit** + **FsCheck** (NFR-MAINT-01/02). No new dependencies are introduced. The Farmer rendering/movement work uses existing Stardew Valley game APIs already on the `Dayswork` project's reference set.

## TS-U13-02 — Worker rendering via the `Farmer` system (FD-Q5=B)
The worker is a `StardewValley.Farmer` drawn by `FarmerRenderer`, with tool swings played through `FarmerSprite.animateOnce(...)`. Verified swing frame sets: heavy tools (axe/pickaxe) rows R12/R9/R7; watering can R10/R5/R8/R11; scythe/melee R5/R6/R7 — per Stardew Valley Wiki (Modding:Farmer sprite). The held tool is the Farmer's real `CurrentTool`.

## TS-U13-03 — Movement: reuse the game's tile pathfinding, drive the Farmer manually
Rather than reimplement A*, U-13 computes the tile route using the game's existing pathfinding and then advances `Farmer.Position` + walk animation along that route each sampled tick. Rationale: navigation parity with vanilla, far less code, and the worker still behaves like an in-world character. This **replaces** the U-10 `PathFindControllerAdapter` for the Farmer (PathFindController drives `NPC`/`Character` movement, which a non-player `Farmer` does not honor cleanly).

## TS-U13-04 — Worker draw/update integration — DEFERRED to NFR Design
How the standalone Farmer gets ticked and drawn with correct depth sorting is an internal pattern choice, recorded here and **resolved in NFR Design**:
- **(a) Manual render hook (recommended starting point):** draw the worker ourselves each frame, sorted by world Y, keeping it out of every game-managed collection (honors SAFE-U13-03 / BR-WORKER-01). Cost: we replicate depth sorting.
- **(b) Register in `location.characters`:** gains free depth-sorted drawing + update ticking, but risks the game treating it as a real character (schedules, dialogue, serialization, player interaction) — which fights the never-serialize rule.

Recommendation: start with (a); accept the BR-WORKER-03 cosmetic fallback if exact depth parity proves costly. Confirm in NFR Design.

## TS-U13-05 — Appearance randomization from character-creation field ranges
`WorkerAppearance` is randomized using the valid ranges the New Game character-creation menu exposes (skin, hairstyle + color, shirt, pants + color, accessory, eye color, gender). No external art assets; all values reference base-game appearance data.

## TS-U13-06 — Stuck thresholds from existing config
`StuckInitialThresholdMinutes` and `StuckPostTeleportThresholdMinutes` (both default 10) already exist on `ConfigSnapshot` (C-14). GMCM exposure is U-16; U-13 only reads them.
