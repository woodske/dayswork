# Dayswork — AI context

Single-player Stardew Valley SMAPI mod. The player builds a farm office
(`Bindicle.Dayswork_Office`) and hires an NPC farmhand from it. The worker spawns at the farm
entrance each morning, **walks** the farm doing the contract's configured work (water/harvest
crops, collect fruit, animal care, clear rocks/weeds/grass/trees, plus a full managed-crop
lifecycle), deposits output into the player's chests, and leaves. Payment is **upfront** for a block
of worker energy. Constraints baked into the design: progression-aware (worker inherits the
player's tool levels), safe (items are never lost — undelivered output goes to the office output
chest / shipping bin), single-player only, and the worker must physically walk (no warping except
stuck-recovery and building doors).

See **`docs/architecture.md`** for the full subsystem map and the shift loop. Start any code task
from **`Dayswork/ModEntry.cs`** — it's the hand-wired composition root (no DI container); every
service and the SMAPI event it hangs off is visible there.

## Build / deploy

- `dotnet build Dayswork/Dayswork.csproj` (or build the solution). Target is **net6.0**.
- `Pathoschild.Stardew.ModBuildConfig` auto-resolves the Stardew/SMAPI references and, with
  `<EnableModDeploy>true</EnableModDeploy>`, **auto-copies the built mod into the Stardew `Mods/`
  folder** on every build — no manual deploy step. `manifest.json`, `i18n/`, and `assets/` are
  copied to output.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — warnings fail the build. Nullable is on,
  LangVersion 10, ImplicitUsings on.
- Release: set `<EnableModZip>true</EnableModZip>` (off by default); Release also strips PDBs.
- Running it: launch the game through SMAPI; the mod loads on a single-player save.

## Hard architectural rules (enforced in code)

1. **Core purity.** `Dayswork.Core/` must reference **zero** SMAPI/Stardew types — it has no
   `ModBuildConfig` reference, only `Dayswork/` does. Put game-touching code in `Dayswork/`;
   put pure logic (pricing, energy, state machine, planning, DTOs) in Core where it's unit-tested.
2. **No Harmony.** The mod uses SMAPI events only — there are no Harmony patches and no
   `<EnableHarmony>`. Don't add one without a hard reason; prefer an event hook.
3. **Single active contract.** At most one Active/Paused contract at a time (enforced in the hiring
   flow and assumed by the scheduler).
4. **Items are never lost.** Every deposit/overflow path falls back so collected items end up
   somewhere safe (chest → office output chest → shipping bin). Preserve this when touching deposit
   or shift-stop code.
5. **The worker is removed before save.** `CalendarHandlers.OnSavingHook` runs before persistence
   and despawns the `FarmhandNpc`; never let it serialize into the save.
6. **Single-player only.** Guard new entry points with `MultiplayerGuard.IsMultiplayer()`.
7. **Verify game content — never guess.** Warp/entrance tiles, item ids, qualified ids, building
   ids, category numbers, event/data keys, animal data, etc. must be confirmed against the actual
   game data or a decompile before use — not recalled from memory. When you investigate and confirm
   a piece of content, **record it in `docs/` so it never has to be looked up again** (see
   "Verified game-content references"). Treat anything not yet confirmed as unknown.

## Where things live

- `Dayswork/Orchestration/` — the shift engine (`ShiftOrchestrator.*` partials), day-start
  scheduler, work scanning, animal handling. All cross-location movement (building doors,
  expansion hops, store trips) runs through one primitive: `Travel.cs` (`TravelPlan` +
  `TravelRunner`), with the completion dispatch in `ShiftOrchestrator.Travel.cs`.
- `Dayswork/Integration/` — building definition + interaction, persistence, config/GMCM, chest and
  shop resolution.
- `Dayswork/UI/` — the hub-and-spoke hiring menus + a small layout toolkit (`UI/Layout/`).
- `Dayswork/Worker/` — the NPC, movement driver, tool animation.
- `Dayswork/Compat/` — SVE / farm-expansion support (vanilla path is a no-op).
- `Dayswork.Core/` — domain, capabilities, pricing, energy, shift state machine + planner,
  inventory/deposit, persistence DTOs.
- `docs/farm-warps/` — per-farm entrance/warp tile reference (vanilla + SVE).

## Verified game-content references

Confirmed game content lives under `docs/` so it's looked up once, not re-derived each session
(per hard rule 7). Add to these (or start a new file) whenever you confirm content against game
data or a decompile, and note where you confirmed it.

- `docs/farm-warps/` — farm entrance/warp + interior-door tiles, vanilla and SVE.

Hard-coded ids that are already verified in code (keep them centralized when you touch them):
the office building/chest ids in `Dayswork/Integration/HiringBuilding.cs`, the animal-product
forage ids/categories in `Dayswork/Orchestration/WorkAreaScanner.cs`, and tool/build-material item
ids in `HiringBuilding.BuildData`.

## SDV-1.6 landmines (these cause bugs if forgotten)

- `PathFindController` lives in **`StardewValley.Pathfinding`**; check `pathToEndPoint` for null.
- Use `QualifiedItemId` + `ItemRegistry.Create`; bad ids yield a fallback **"Error Item", not
  null** — always guard before shipping/depositing.
- Vanilla harvest/tool/grass/tree APIs mutate `Game1.player` and enqueue HUD messages even when the
  **worker** acts — wrap worker actions to snapshot/restore player state, redirect gained items to
  the worker buffer, and trim HUD messages (see `InvokeTaskActionGuarded`).
- `crop.harvest()` doesn't clean up `dirt.crop`; `ResourceClump.performToolAction` calls
  `destroy()` internally and returns a bool (never destroy manually); felled trees pass through a
  stump phase and spawn debris *after* the fall animation (delayed debris sweep).
- Check `chest.GetMutex().IsLocked()` before writing to a chest.
- `BuildingDrawLayer` can't be conditioned in this version → conditional building overlays are
  C#-rendered in `Display.RenderedWorld`.
- `FarmerSprite.StopAnimation` is a no-op while `pauseForSingleAnimation` is true — clear the flag
  first.

## Current state

Builds clean and runs. Working today: build the office and hire from its bulletin board; the
hiring flow (tasks, zone-draw work scope, output chests, energy tier, task priority, one-time vs
recurring schedule, managed crops); the full shift loop (animal care, crops, fieldwork, managed-crop
planting with auto-buy, multi-trip deposits, overflow safety, stuck recovery, 8pm cap, sleep
settle); save/load persistence; evening office lighting/smoke; optional GMCM config; and SVE
expansion compatibility. The farmhand uses a **placeholder** Marnie sprite/portrait (custom art is
post-v1). Dev tooling (verbose logs + console commands like `dayswork_end_shift`) is gated behind
`DevLog.Enabled`, off for release.
