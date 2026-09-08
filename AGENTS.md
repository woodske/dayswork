# Dayswork — AI context

Stardew Valley SMAPI mod, single-player and host-authoritative co-op. A player builds farm offices
(`Bindicle.Dayswork_Office`) — **any number of them, one farmhand each** — and hires an NPC farmhand
from each. A worker spawns just outside its own office's door each morning, **walks** the farm doing
that contract's configured work (water/harvest crops, collect fruit, animal care, clear
rocks/weeds/grass/trees, plus a full managed-crop lifecycle), deposits output into the owner's
chests, and returns to its office. Payment is **upfront** for a block of worker energy. Constraints
baked into the design: progression-aware (worker inherits the owner's tool levels), safe (items are
never lost — undelivered output goes to the office output chest / shipping bin), the engine runs on
the host alone (all peers must run the same protocol version), and the worker must physically walk
(no warping except stuck-recovery and building doors).

See **`docs/architecture.md`** for the full subsystem map and the shift loop. Start any code task
from **`Dayswork/ModEntry.cs`** — it's the hand-wired composition root (no DI container); every
service and the SMAPI event it hangs off is visible there.

`docs/` is split three ways, each folder with its own index:

- **`docs/plans/`** — implementation plans. Status lives *only* in `docs/plans/index.md`, never in
  the plan files. Completed plans are summarised in `docs/plans/archive/` (why the work was done and
  what was decided) and their plan files deleted; archiving is done by the `archive-plans` skill.
- **`docs/analysis/`** — research, feasibility studies, and weighed design tradeoffs. No status; the
  value is the reasoning. Indexed in `docs/analysis/index.md`.
- **`docs/game-data/`** — verified Stardew/SVE content (see "Verified game-content references"
  below). Indexed in `docs/game-data/index.md`.

**Update `CHANGELOG.md` whenever a change lands that a player would notice** — a new feature, a
behavior change, or a bug fix. Add it to the `## Unreleased` section under `Added` / `Changed` /
`Fixed`. These entries are **public-facing**: one or two plain sentences describing what changed for
the player, no type or file names, no internal rationale. Refactors, docs, tests, and anything that
never shipped in a release get no entry.

## Local game/source paths

- Stardew Valley install: `X:\Steam\steamapps\common\Stardew Valley`
- SMAPI logs: `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` (resolves to `C:\Users\kwood\AppData\Roaming\StardewValley\ErrorLogs\`)
- Stardew Valley Expanded source: `C:\Users\kwood\Repos\StardewValleyExpanded`

Use `docs/game-data/game-content-search.md` for the fastest way to search/parse the base game and SVE
trees. Per hard rule 7, verify ids, tiles, qualified ids, event keys, data fields, and runtime API
behavior against these local files, runtime data, or a decompile; then record any newly confirmed
facts under `docs/game-data/` and add a row to its `index.md`. Update `docs/game-data/game-content-search.md` if any new search techniques are used.

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
2. **SMAPI events first; narrowly scoped Harmony exceptions.** Prefer events and direct game APIs.
   Use Harmony when a verified game-method boundary removes substantial complexity or improves
   correctness. The approved exception (2026-09-08) is host-only attribution of worker-felled tree
   drops around `Tree.tickUpdate`; see `docs/plans/dayswork-2.0-review-fixes.md`, R6. Keep patches in
   `Dayswork/`, preserve vanilla behavior for unrelated calls, and test item fidelity, ownership,
   and lifecycle cleanup. This exception uses before/after hooks with exception-safe cleanup,
   not a transpiler or global loot/XP/admission rewrite. Networking and XP retain their existing
   event/request and Sponsor paths.
3. **One contract per office; N offices ⇒ N contracts.** An office holds at most one
   Active/Paused contract and runs at most one live shift; the farm may have any number of offices.
   The contract belongs to its office — it lives in that building's `modData`, is keyed by
   `Building.id` (`Contract.OfficeId`), and is destroyed with the building. Never resolve "the
   farm's office": ask `OfficeResolver` for a specific one, by id or by the clicked tile.
4. **Items are never lost — and never degraded.** Two invariants on every collected item/material:
   - *Never lost.* Every deposit/overflow path falls back so output ends up somewhere safe
     (chest → office output chest → shipping bin). Preserve this when touching deposit or shift-stop code.
   - *Full fidelity preserved through every hop.* An item the worker collects must reach its
     destination **identical** to what the player would have picked up — same **quantity**, **quality**
     (base/silver/gold/iridium), and **distinct identity/traits**: flavored-preserve id + type (Sturgeon
     Roe, Blueberry Wine, flavored honey…), color, and any other `Object` traits. The collect → buffer →
     planner → deposit/overflow chain must carry these end-to-end; when a trait can't be reconstructed
     from the qualified id alone, capture the real item and clone it back (see the per-shift
     `FlavorItemRegistry` + `BufferedItem.FlavorId`). Quality/flavor/etc. are also part of the
     consolidation key so distinct variants never merge. When you add a new collect path or touch the
     deposit pipeline, verify nothing silently genericizes an item.
5. **The worker is removed before save.** `CalendarHandlers.OnSavingHook` runs before persistence
   and despawns the live `FarmhandNpc` (via `ShiftOrchestrator.StopForSleepAndSettle`); never let one
   serialize into the save.
6. **Host-authoritative multiplayer.** All connected players must run the same Dayswork
   `ProtocolVersion`. **Clients never run a shift loop, write save data or office `modData`, spend
   money, or mutate the world — they send requests.** The engine (day-start scheduler, every
   `ShiftOrchestrator`, sleep-settle, persistence, money, XP, chest ensure, demolition handling)
   runs only where `Authority.IsHost`; menus, overlays, asset loading, and interaction run
   everywhere. A contract change of any kind — the host's own included — goes through
   `ContractRequestClient` → `ContractRequestHandler`, which on the host's own computer
   (`Authority.HostIsInThisProcess`, true for split-screen too) is a direct call, so single-player
   and co-op share one commit path and one set of checks. Anything the host cannot safely share
   (a peer without the mod, or with a different protocol version) suspends the mod for everyone,
   or kicks that peer when `KickIncompatiblePeers` is on.
7. **Verify game content — never guess.** Warp/entrance tiles, item ids, qualified ids, building
   ids, category numbers, event/data keys, animal data, etc. must be confirmed against the actual
   game data or a decompile before use — not recalled from memory. When you investigate and confirm
   a piece of content, **record it in `docs/` so it never has to be looked up again** (see
   "Verified game-content references"). Treat anything not yet confirmed as unknown.
8. **The office's owner sponsors its farmhand.** Money, tools, shipping, notices and XP follow
   `Contract.OwnerId`, never `Game1.player`, and they go through **one seam**:
   `Dayswork/Integration/Sponsor.cs`. Nothing else in `Dayswork/` may read or write `Farmer.Money`
   or call `getShippingBin`/`shipItem` — `SponsorSeamLintTests` fails the test run if it does. The
   one deliberate exception is the worker's fake action farmer (`CreateWorkerActionFarmer`), which
   keeps the **host's** `UniqueMultiplayerID` forever: vanilla gates machine collect and tree XP on
   `Farmer.IsLocalPlayer`, which is identity-based, so a sponsor-identity action farmer would break
   Manage Machines outright.

## Code conventions

- **No ceremony.** Don't add an interface, wrapper class, or single-method "policy"/"evaluator"
  class unless at least two implementations or call sites need it. Pure stateless logic is a
  static method. Prefer extending an existing class over creating a new file.
- **Testing policy.** Tests are *required* for persistence formats/migrations, pricing and money
  math, and item-routing invariants (items are never lost) — plus a regression test when fixing a
  bug. Per-feature ritual coverage is not required; the shift engine itself is verified by
  in-game play-testing (see `DevLog.Enabled` + the `dayswork_*` console commands).
- **Core placement.** Before adding a game-state snapshot type to Core, check the decision logic
  is complex enough to justify the reader → planner → executor round trip. A single `if` belongs
  in `Dayswork/` next to the game call.
- **Logging.** Two mechanisms — pick the right one:
  - **Operational logs** (errors, warnings, skip/skip-reason events the player or a bug report
    would care about): use `ModEntry.ModMonitor.Log(message, DevLog.WarnLevel)`. Always-on;
    `DevLog.WarnLevel` is `Debug` during dev and `Warn` in release so the same line doesn't spam
    in dev mode but surfaces clearly in release SMAPI logs.
  - **Dev-only diagnostic logs** (verbose field state, action counts, internal trace info): use
    `DevLog.Log(message, level)`. Gated by `DevLog.Enabled` — silent in release builds. Never
    use `DevLog.Log` for something the player or a support log would need to see.

## Bug Fixing

- When fixing bugs that relate to game behavior, always verify the root cause against the decompiled Stardew Valley game code before proposing a fix; 
  do not rely on assumptions or the explore agent's mental model.

## Where things live

- `Dayswork/Orchestration/` — the shift engine. `ShiftFleet` owns **one `ShiftOrchestrator` per
  live shift** and is the single subscriber to the game events, fanning out to each orchestrator
  **sequentially** — that sequencing is the concurrency model, since each worker's guarded
  vanilla-API beat runs synchronously inside one callback and two workers must never interleave
  their `Game1.player` snapshot/restore. `FleetDay` holds the state shared by the day's shifts
  (`WorkClaimRegistry`, a `ShoppingBudgetLedger` per wallet, the crop-HUD dedup reset).
  `ShiftOrchestrator.*` partials drive the tick loop and the state-machine transitions; **all
  mutable per-shift state lives on `ShiftSession`** (created at shift start, discarded at shift end
  — a fresh session is the reset), which also carries the shift's `OfficeId`/`OwnerId` and holds the
  per-shift `ManagedShoppingCoordinator` (store trips) and `DepositTripRunner` (chest/bin deposit
  trips). Day-start scheduler, work scanning, and animal handling live alongside. All cross-location movement (building doors, expansion hops, store trips) runs
  through one primitive: `Travel.cs` (`TravelPlan` + `TravelRunner`), with the completion
  dispatch in `ShiftOrchestrator.Travel.cs`.
- `Dayswork/Integration/` — building definition + interaction, persistence, config/GMCM, chest and
  shop resolution. `Sponsor` is the owner-identity seam (hard rule 8) — wallet, tools, shipping,
  XP — and `OwnerNotifier` is its notice counterpart (HUD here / message to the owner / log when
  they are away). `OfficeResolver` finds offices (by id, by tile, or all of them); `OfficeModData`
  owns the office's `modData` keys and `OfficeContractPersistence` is the only thing that writes
  them — including the one-time 1.x → 2.0 contract adoption.
- `Dayswork/Net/` — the multiplayer layer (hard rule 6). `ContractRequestClient` is the only way to
  change a contract; `ContractRequestHandler` is the host side that decides and commits;
  `DaysworkNetwork` owns the peer handshake, the incompatible-peer policy, and message dispatch;
  `DaysworkSuspension` holds the stand-down state and draws its banner; `MenuSnapshotCache` holds
  what a remote client cannot see of the host's world. The pure parts — message DTOs, the commit
  and action validators, the request-id dedupe — live in `Dayswork.Core/Net/` and are unit-tested.
- `Dayswork/UI/` — the hub-and-spoke hiring menus + a small layout toolkit (`UI/Layout/`).
- `Dayswork/Worker/` — the NPC, movement driver, tool animation, and `FarmhandAppearance` (the
  sprite/portrait/paint-mask assets and the painted colour variants).
- `Dayswork/Compat/` — SVE / farm-expansion support (vanilla path is a no-op).
- `Dayswork.Core/` — domain, capabilities, pricing, energy, shift state machine + planner,
  inventory/deposit, persistence DTOs.
- `docs/game-data/` — verified game content, indexed in `docs/game-data/index.md`; includes
  `farm-warps/` (per-farm entrance/warp tiles, vanilla + SVE).
- `docs/analysis/` — research and design-decision records; `docs/plans/` — implementation plans.

## Verified game-content references

Confirmed game content lives under `docs/game-data/` so it's looked up once, not re-derived each
session (per hard rule 7). **`docs/game-data/index.md` lists every file with a one-line summary —
read it before assuming something is unverified.** Add to these (or start a new file) whenever you
confirm content against game data or a decompile, note where you confirmed it, and add an index row.
The load-bearing ones are called out below.

- `docs/game-data/farm-warps/` — farm entrance/warp + interior-door tiles, vanilla and SVE.
- `docs/game-data/game-content-search.md` — local Stardew/SVE paths, folder maps, and search/parsing tips for
  base `.xnb`/DLL content, SVE Content Patcher packs, C# source, and `.tmx` maps.
- `docs/game-data/zoom-and-viewport.md` — how the frozen farm view zooms/pans (`Game1.options.desiredBaseZoomLevel`, viewport recompute, `getMouseX(false)` zoom-awareness, MonoGame `TouchPanel` for pinch).
- `docs/game-data/machines.md` — `Data/Machines` schema + machine runtime API (`GetMachineData`, `PlaceInMachine`/`AttemptAutoLoad`, `MachineDataUtility`, `readyForHarvest`/`heldObject`) backing the Manage Machines feature (built 2026-06-19; reader = `Dayswork/Orchestration/MachineReader.cs`); archived plan in `docs/plans/archive/machine-and-pond-management.md`. Its "Fish ponds" section verifies the `StardewValley.Buildings.FishPond` API (`output` NetRef, `tileX/tileY` identity, direct-null collect, 5×5 footprint) backing Manage Fish Ponds (built 2026-06-23; reader = `Dayswork/Orchestration/FishPondReader.cs`); archived plan in the same file.
- `docs/game-data/farmhand-art.md` — farmhand sprite/portrait dimensions, frame layout, verified NPC/farmer animation constants, and the decision to keep body animation separate from tool/effect sprites.
- `docs/game-data/fences-and-gates.md` — `StardewValley.Fence` gate API (`isGate`, `gatePosition` 0/88, `health > 1f`, `isPassable`, `toggleGate`, `updateWhenCurrentLocation` auto-close rule) backing the worker's open-gates-while-pathing logic in `Dayswork/Worker/WorkerMovementDriver.cs`.
- `docs/game-data/debris-and-drops.md` — which `Game1.create*Debris` overloads route loot to `Game1.currentLocation` vs the passed `location`; the `ResourceClump.destroy()` leak (hardwood/stone spawn at the *player's* location, not the clump's) and the `InvokeTaskActionGuarded` sweep that recovers it.
- `docs/game-data/sound-cues.md` — the **worker-sound invariant** (every worker action emits the player's sound, gated on `Game1.player.currentLocation == location`; silent off-location), the verified cue-per-action table, how to enumerate cue names from the XACT sound bank, and the `IsLocalPlayer` gotcha (vanilla machine-collect `"coin"` won't fire for the fake worker farmer).
- `docs/game-data/chests.md` — `Chest` capacity/special-type API (`SpecialChestTypes.BigChest` → `GetActualCapacity()` 70, serialized so it persists) and why `BuildingData.Chests` can't express capacity — backing the office porch chests being upgraded to Big Chests in `Dayswork/Integration/OfficeChestService.cs`; also the wood/stone/big chest id table (`232` is the Stone Chest, not the Big Chest).
- `docs/game-data/time-and-pacing.md` — verified game-clock constants (`realMilliSecondsPerGameMinute = 700`; the `gameTimeInterval > 7000 + ExtraMillisecondsPerInGameMinute*10` ten-minute trigger; ~42 ticks/in-game-minute at 60 UPS) and the tile→minute walk-time conversion backing `Dayswork.Core/Shifts/ShiftClockEstimator.cs` (time-aware wrap-up, #5). Confirmed 2026-07-07.
- `docs/game-data/multiplayer-and-ownership.md` — **`Farmer.IsLocalPlayer` is identity-based**
  (`UniqueMultiplayerID == Game1.player`'s) and vanilla gates machine collect (`Object.cs:4626`) and
  tree XP/stats (`Tree.cs:1498/1514`) on it, so the worker's fake action farmer must keep the
  host's id; where `gainExperience` routes XP for local / remote-online (game message 17) / offline
  (silently dropped) farmers; `Building.owner` is set in `buildStructure`; `Building.id` is a
  serialized, synced per-instance `NetGuid` (survives a move, dies with demolition) and
  `BuildingData.BuildCondition` defaults to always-available — together the basis for keying a
  contract to its office; the exact `experiencePoints` / skill-level / `newLevels` / `MasteryExp`
  fields a worker-beat guard has to restore, and that `CreateFakeEventFarmer` copies **no** XP (so a
  fresh action farmer's totals are the beat's delta); painting needs a `Data/PaintData` entry, not
  just a `_PaintMask`. Backs `docs/plans/dayswork-2.0.md`. Confirmed 2026-09-07.
- `docs/game-data/painting.md` — `BuildingPainter.Apply` / `BuildingPaintColor`: the exact
  red/lime/blue mask colours matched by **equality**, the flat-index rule (**a mask must be exactly
  the size of the sheet it paints**), the two cases where `Apply` returns null (and caches that
  failure for the session), and why the hue/saturation/lightness numbers read as absolutes. Also the
  full vanilla `Data/PaintData` table — every entry reuses the region names `Building`/`Roof`/`Trim`,
  already translated in `Strings/Buildings`, so a custom building needs no string edit — and how the
  office's mask is derived from its sprite. Backs both the worker palettes and the paintable office.
- `docs/game-data/pathing.md` — the worker passability probe (`IsTilePassableForWorker`, inset `+1/62` rect), the verified `isCollidingPosition(character: null, …)` block table (**FarmAnimals do NOT block** — the animal loop is skipped when `character` is null), the Core `GridPathfinder`/`PassabilityGrid` BFS extraction (N,E,S,W tie-break is load-bearing), and the per-shift `LocationPassabilityCache` (which call sites are cached vs. live, the staleness contract, and the three invalidation mechanisms). Built 2026-07-07.

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
- **Worker sounds don't auto-fire.** Every worker action must emit the player's sound, gated on
  `if (Game1.player.currentLocation == location) location.playSound(cue, tileVec)` — audible
  on-location, silent off-location. Many vanilla APIs that "play the sound for you" gate it behind
  `who.IsLocalPlayer`, which is false for the worker's `CreateFakeEventFarmer()` (e.g.
  `Object.CheckForActionOnMachine`'s collect `"coin"`), so the sound silently doesn't play — emit it
  yourself. When adding any collect/work path, wire its cue per `docs/game-data/sound-cues.md`.

## Current state

Builds clean and runs. **All of 2.0 — Phases 0–5 — landed 2026-09-07** (branch `dayswork-2.0`;
every phase still owes its in-game smoke pass, deferred until the whole thing was built).

*Phase 1* — the farm may hold **any number of offices, one farmhand each**. A contract belongs to its
office — stored in that building's `modData` under schema v4, keyed by `Building.id`, carrying an
`OwnerId`/`OfficeId`/`Revision`. `ShiftFleet` runs one `ShiftOrchestrator` per live shift, fanned out
sequentially, with a per-day `WorkClaimRegistry` arbitrating overlapping scopes and a
`ShoppingBudgetLedger` per wallet keeping two workers from spending the same gold. A 1.x save's
contract is adopted onto its office on first load. Demolishing an office ends its shift and takes its
contract (no refund). Everything below still holds, now per office.

*Phase 2* — the office's **owner sponsors** its farmhand, through one seam:
`Dayswork/Integration/Sponsor.cs`. Wallet (`team.GetMoney(owner)` — one code path for shared and
separate wallets), tools (`ToolLevelReader.ReadSnapshot(Sponsor.ResolveOrHost(ownerId))`), shipping
(`Sponsor.ShipItem` / `ShippingBin`), notices, and XP all follow the contract's `OwnerId`. **Nothing
outside `Sponsor` may touch `Farmer.Money`, `getShippingBin` or `shipItem`** — enforced by
`Dayswork.Tests/Lint/SponsorSeamLintTests.cs`. XP is harvested as a **difference** around each
guarded beat (host restore + fresh action-farmer read, see `WorkerBeatXpSnapshot`), banked in the
per-shift `XpLedger`, and flushed at batch boundaries and shift end; it is dropped when the
contract's `GrantExperience` preference is off or the owner is not connected. Fieldwork XP
(tree/rock) now reaches the owner — it never did before. The worker's fake action farmer keeps the
**host's** identity forever; see hard rule 8 below. In single-player the owner is always the local
player, so all of this is behaviour-preserving except the new fieldwork XP.

*Phase 3* — each contract picks one of **ten worker colour palettes** (Preferences → Appearance, with
a live sprite preview). A palette is not a drawn sheet: `assets/farmhand_PaintMask.png` marks the
cap / shirt / overalls, and `Dayswork/Worker/FarmhandAppearance.cs` serves
`Characters/DaysworkFarmhand_<key>` as the base sheet run through vanilla's own `BuildingPainter`
(palettes are pure data in `Dayswork.Core/Domain/WorkerAppearance.cs`; the API is verified in
`docs/game-data/painting.md`). Because each variant is a real content asset, appearance travels as
nothing but a texture name — `FarmhandNpc.getTextureName()` derives it from the NPC's synced
`modData`, which also now carries the worker's name and (5 %-quantised) energy.

*Phase 4* — **host-authoritative multiplayer** (hard rule 6). `Authority` replaces
`MultiplayerGuard`; the engine is host-only, and every contract change — the host's own included —
goes through `ContractRequestClient` → `ContractRequestHandler`, a direct call when the host is in
this process (`Authority.HostIsInThisProcess`, true for split-screen) and a message otherwise. The
decision is pure and unit-tested per rejection code (`Dayswork.Core/Net/ContractCommitValidator`,
`ContractActionValidator`), and `RequestIdCache` makes a retried commit idempotent so a timed-out
one-time contract cannot be charged twice. Any player may open any office;
`UI/OfficeViewerRoles` decides what they may do (owner: everything; host on another player's
office: Pause / Cancel, plus Claim when that owner's save slot is gone; anyone else: read-only). The
two pickers a client cannot populate from its own world — expansion work locations, off-farm machine
input chests — are **restricted, not synced**, filled from a `MenuSnapshotResponse` and labelled
"waiting for the host" until it arrives. Upgrades became **per owner** (save schema v3; a v1/v2 save
migrates onto the host). Shift notices reach their owner through `OwnerNotifier` (HUD here, message
there, log when they are away). A peer that cannot run Dayswork **suspends** the mod for everyone —
live shifts end, nothing spawns, a chat line readable without the mod plus an on-screen banner say
why — or is kicked when the new `KickIncompatiblePeers` config is on.

*Phase 5* — the office is **paintable** at Robin's, like a barn or a coop.
`assets/farmhand_office_PaintMask.png` marks its walls / roof / trim, served under the one name
vanilla will ask for (`Mods/Bindicle.Dayswork/Building_PaintMask`, i.e.
`textureName() + "_PaintMask"`), and `HiringBuilding` adds the `Data/PaintData` entry without which
the carpenter menu refuses the building outright. The three regions reuse **vanilla's own** names
(`Building` / `Roof` / `Trim`), so their menu labels come from the base game's already-translated
`Strings/Buildings:Paint_Region_*` and Dayswork ships no string of its own; the brightness pairs are
the Log Cabin's. The colour needs neither persistence nor sync — `netBuildingPaintColor` is both.
`HiringBuildingOverlayRenderer` now draws the evening glow and smoke from each office's own
`building.texture.Value` rather than reloading the raw sheet, so a repainted office and its lit
window can never disagree. See `docs/game-data/painting.md` for the mask's derivation.

Working today: build an office and hire from its bulletin board; the
hiring flow (tasks, zone-draw work scope, output chests, energy tier, task priority, one-time vs
recurring schedule, managed crops, **Manage Machines**, **Manage Fish Ponds**); the full shift loop
(animal care, crops, fieldwork, managed-crop planting with auto-buy, **machine collect/reload**,
**fish-pond collect**, multi-trip deposits, overflow safety, stuck recovery, 8pm cap, sleep settle);
save/load persistence; per-office evening lighting/smoke; repainting the office at Robin's;
optional GMCM config; and SVE expansion compatibility. **Manage Machines** (2026-06-19) is built, unit-tested, and **passed its in-game smoke
pass (milestone 8) on 2026-06-28 — release-ready**: worker collect/reload, fish-smoker (fish+coal) and
dehydrator (×5) loads, flavored-roe round-trip, filtered loads, and the **per-group fetch-first
single-visit** workflow (worker fetches a group's inputs in one chest trip, then visits each machine
once to collect→reload) all verified in-world. A group's **input chest may be in any location**
(2026-07-06): a cross-location chest triggers a fetch excursion routed through the farm hub
(smoke-passed 2026-07-07, along with auto-grabbers as input chests). See
`docs/plans/archive/machine-and-pond-management.md` for the decisions + limitations. **Manage Fish Ponds**
(2026-06-23, collect-only) is built, unit-tested, and **passed its in-game smoke pass on
2026-07-07 — release-ready**; see the same archive file. Collected output keeps its **flavored/colored identity** (Sturgeon Roe,
blueberry wine, flavored honey…) end-to-end via the per-shift `FlavorItemRegistry` +
`BufferedItem.FlavorId` (capture-and-clone; benefits machine output too). The worker is
**player-nameable** (`ContractPreferences.WorkerName`, set via the vanilla `NamingMenu` from the
Preferences spoke, persisted and shown in HUD notices and the Manage list). `DepositTripRunner`
holds ~3s when the player has the destination chest open before falling through to overflow.

**Known Phase-4 limitation:** managed-crop shift notices (`CropHudNotifier` — shopping, skipped
plantings, missing tools) stay on the host's HUD rather than reaching the contract's owner. They are
deduplicated across every one of the day's workers, so they have no single owner to address;
routing them would mean threading an owner id through that whole pipeline and giving up the shared
dedup. Everything `ShiftOutcomeDispatcher` raises does reach its owner.

Dev
tooling (verbose logs + console commands like `dayswork_end_shift`, `dayswork_debug_machines`,
`dayswork_debug_leaks`) is gated behind `DevLog.Enabled`, off for release.
