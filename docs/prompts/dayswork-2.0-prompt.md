# Dayswork 2.0 — architecture planning brief

You are architecting the implementation plan for **Dayswork 2.0**, a major version of a Stardew
Valley SMAPI mod (currently 1.2.1, `Bindicle.Dayswork`). Produce a phased implementation plan, not
code. Repo root: `C:\Users\kwood\Repos\dayswork`.

**Read first, in this order:** `AGENTS.md` (hard architectural rules — they still apply),
`docs/architecture.md` (subsystem map; note the correction below), `Dayswork/ModEntry.cs` (the
hand-wired composition root — every service and its SMAPI event is visible there), and
`docs/analysis/multiplayer-readiness.md` (a prior analysis; its *facts* are sound but its
*recommendation* — a single shared worker, host-only — is explicitly superseded by this brief).

---

## What Dayswork is today

Single-player. The player builds one farm office (`Bindicle.Dayswork_Office`) and hires one NPC
farmhand from its bulletin board. The worker spawns each morning, **walks** the farm doing the
contract's configured work (crops, animals, fieldwork, managed-crop lifecycle, machines, fish
ponds), deposits output into the player's chests, and returns. Payment is upfront for a block of
worker energy. Hard invariants: items are never lost or degraded, the worker is despawned before
save, and there are zero Harmony patches.

Multiplayer is currently *disabled*, not supported: `MultiplayerGuard.IsMultiplayer()` returns
`Context.IsMultiplayer`, and it gates the bulletin-board interaction and the day-start scheduler.

---

## The 2.0 goals

1. **Multiple farmhands — one per office building, no enforced limit.** Build another office, get
   another worker.
2. **Multiplayer compatibility.** Each player can build and own offices; a player's farmhands keep
   working while that player is offline, as long as the host is playing.
3. **Paintable office buildings** via the vanilla Paint Bucket.
4. **Customizable farmhand appearance** (color variation is sufficient).
5. **A toggle for whether worker labor grants the owning player experience.**

---

## Decisions already made — treat these as fixed

- **Topology: N identical offices, each housing exactly one worker.** Do *not* design a hub +
  satellite-cabin split, and do not allow multiple workers per office. Each office keeps its own
  Input/Output porch chests.
- **Do not use the word "cabin"** in user-facing text, type names, or building ids. In Stardew
  multiplayer `Cabin` is a real building type that houses a real human player (`Cabin.owner`,
  `AssignFarmhand`, `DeleteFarmhand`), and the game's own word for a joining player is "farmhand."
  Keep "office" for the building; the NPC keeps whatever the player names it.
- **All connected players must have Dayswork installed.** This is a hard requirement (see the
  netcode finding below). Design a peer/version handshake, warn loudly, and define safe degradation
  when a modless or version-mismatched client is connected. The mod may keep adding the custom NPC
  to `GameLocation.characters`.
- **Sponsor = the player who built the office** (`Building.owner`). Their wallet is charged, their
  tool levels are inherited, and their XP toggle applies.
- **Offline owners: workers keep working — opt-in per contract, defaulting to on.**
- **XP: the contract owner gains experience for all worker tasks**, with a toggle to disable it
  entirely. This must be made deliberate and consistent (see the XP finding — today it is neither).
- **Painting: use the vanilla Paint Bucket path** (`Building.netBuildingPaintColor` plus a
  `_PaintMask` texture), not `BuildingData.Skins`.
- **No worker-count cap and no dedicated performance-tuning phase.** Build cost is the only gate.
  This does *not* excuse the per-worker state extraction below, which is a correctness requirement.

---

## Verified engine facts — confirmed against the decompiled game; do not re-derive or second-guess

Decompiled from `X:\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll`. Per hard rule 7,
record any *additional* facts you confirm under `docs/game-data/`.

### Custom NPCs over the network — why the mod is mandatory for all players

`GameLocation.characters` is `new NetCollection<NPC>()` constructed **without** an `XmlSerializer`
(`GameLocation.cs:247`). That selects the `WriteTypeOf`/`ReadType` path in `NetRefBase`, which writes
only `type.FullName` to the wire and, on receipt, resolves it by scanning
`AppDomain.CurrentDomain.GetAssemblies()` and calling `Activator.CreateInstance`
(`Netcode/NetRefTypes.cs:12-39`, `NetRefBase.createType`). Therefore:

- A client **without** Dayswork gets `null` from `GetType` and hits a bare
  `throw new InvalidOperationException()` inside the netcode read path. That is a disconnect or
  crash, not graceful degradation.
- On clients that **do** have the mod, `FarmhandNpc`'s parameterless constructor **runs**. AGENTS.md
  and the class comment currently say it "exists only for the XML serializer and is never expected to
  run" — that becomes false in multiplayer. It must construct a valid, inert instance.

### Money — `Farmer.Money` throws for any non-local farmer

`Farmer.cs:1997`: the `Money` setter throws `"Cannot change another farmer's money. Use
Game1.player.team.SetIndividualMoney"` whenever `Game1.player != this`. **This applies regardless of
wallet mode**, so it bites the moment the host charges anyone but itself — including in a
shared-wallet save.

Wallet mode is not fixed for the life of a save: `FarmerTeam.useSeparateWallets` is set at co-op
world creation (`CharacterCustomization.cs:1964-1972`) and can be flipped **mid-save** at the Mayor's
Manor via `ManorHouse.SeparateWallets()` / `MergeWallets()` (host-only, applied through
`changeWalletTypeTonight`). The mod must survive that switch.

The fix is uniform, not branched: `FarmerTeam.GetMoney(who)` returns the shared `money` object when
wallets are shared and a per-player `NetIntDelta` when they are separate, so a single code path covers
both. Same for shipping — `Farm.getShippingBin(Farmer who)` returns `who.personalShippingBin` under
separate wallets and `sharedShippingBin` otherwise.

**Current call sites to migrate (~8):** `RecurringContractScheduler.cs:138`,
`ShiftOutcomeDispatcher.cs:56`, `ShopPurchaseService.cs:36,53,95,107`,
`ManagedShoppingCoordinator.cs:242,549`, `HiringFlowCoordinator.cs:146,150,1075,1083`,
`UpgradesMenu.cs:72`.

### Experience — the current behavior is accidental, inconsistent, and leaks to the wrong player

The mod calls `gainExperience` **nowhere**. But `Crop.harvest` hardcodes `Game1.player.gainExperience`
rather than `who.gainExperience` — `Crop.cs:693` (Farming, normal harvest) and `Crop.cs:533,549`
(Foraging, scythe/forage harvest). So **the local player already receives Farming XP from the worker's
crop harvests today.** By contrast, tree and rock work routes through `t.getLastFarmerToUse()`
(`Tree.cs:1092,1500,1516`, `Object.cs:1189,1334`), which resolves to the worker's fake event farmer,
so that XP is silently discarded.

Net effect today: crops grant XP, fieldwork does not, and nobody chose that. In multiplayer,
`Game1.player` on the host means **the host would silently absorb XP earned by another player's
farmhand.** The plan must route XP to the contract owner for all task types and suppress it cleanly
when the toggle is off — which means the `Game1.player` leak in `Crop.harvest` has to be neutralized
either way, within the existing `InvokeTaskActionGuarded` snapshot/restore discipline and without
Harmony.

### Offline players are fully reachable from the host

`Game1.netWorldState.Value.farmhandData` holds complete `Farmer` objects for disconnected players —
net-synced and host-readable — exposed via `Game1.getAllFarmhands()` and `Game1.getOfflineFarmhands()`
(`Game1.cs:10967-10992`). `Farmer.CreateFakeEventFarmer()` (`Farmer.cs:9189`) is an instance method
that copies `UniqueMultiplayerID` and appearance, so the host can build a correct action-farmer for an
absent owner. Note the mod currently calls `Game1.player.CreateFakeEventFarmer()`
(`ShiftOrchestrator.TaskActions.cs:641`) — that single call site is the seam where sponsor identity
enters every guarded worker action. An offline owner's tool levels are frozen at their last
disconnect, which is acceptable.

### Building identity, ownership, and synced storage

- `Building.id` is a `NetGuid` and `Building.owner` is a `NetLong` holding the `UniqueMultiplayerID`
  of whoever built it (`Building.cs:38-39, 140-141`). Together these are the natural key for "one
  worker per office, owned by its builder" — no parallel bookkeeping needed.
- **`Building.modData` is a net-synced field** (`Building.cs:407` — `.AddField(modData, "modData")`)
  *and* it persists in the save. This is a strong candidate for per-office contract storage: it gives
  sync, per-building identity, and correct demolish/move semantics for free, with only the host
  writing and every client reading. Weigh it against the alternative below.
- SMAPI's `Helper.Data.WriteSaveData` is main-player-only, so there is **no per-player save data**.
  The alternative is the host holding every contract keyed by `UniqueMultiplayerID` and shipping
  read-only views over `Helper.Multiplayer.SendMessage`.
- Recommended split to evaluate: durable contract data in `Building.modData`, volatile per-shift
  status (phase, energy remaining, current task text) over mod messages. `modData` writes are net
  deltas, so write on edit, never per tick.
- Painting: `Building.netBuildingPaintColor` drives
  `BuildingPainter.Apply(texture, "<Texture>_PaintMask", color)` (`Building.cs:871`). This requires
  authoring a `_PaintMask` texture asset for the office. `Building.cs:434` lazily creates the
  `BuildingPaintColor` when null.

### What a client can and cannot see — this defines the limits of client-side UI

`Multiplayer.isActiveLocation` (`Multiplayer.cs:1370-1385`) returns `true` unconditionally on the
host. On a client it returns true only for the root of the location the client is standing in, or for
an always-active location. What is always active:

- **The Farm always is** — `isAlwaysActive.Value = true` is set in the `Farm` constructor
  (`Farm.cs:161`).
- **So are all of its building interiors.** `_GetActiveLocationsHere` (`Multiplayer.cs:1286-1302`)
  recurses through `location.buildings` and yields each `building.GetIndoors()` for any always-active
  parent. Coops, barns, sheds, the greenhouse, and the Dayswork office interiors and porch chests are
  therefore continuously synced to every connected client.
- **Shop stock needs no sync at all.** `ShopStockReader` reads `ShopBuilder.GetShopStock()` and falls
  back to `Data/Shops` — both are data assets, not location state, so it is accurate on a client
  anywhere in the world.

**Consequence: most of the hiring UI is already client-safe.** Zone-draw over the farm,
`ChestResolver.GetAllChests` (which walks farm objects plus building interiors), the machine picker,
and the fish-pond picker all read state a client already has. A client can build an accurate
`ContractDraft` without any new sync.

**The two gaps are narrow and specific:**

1. **SVE expansion locations are not synced.** Verified in SVE's own data — `Custom_GrandpasShedGreenhouse`
   and `Custom_GrandpasShedOutside` are both `"AlwaysActive": false` in
   `[CP] Stardew Valley Expanded/code/Locations/LocationsData.json`. Dayswork treats that greenhouse as
   a first-class work location (`SveExpansionProfile.cs:90-99`), so a client cannot read its crops or
   chests unless physically standing in it.
2. **Off-farm machine input chests are not synced.** The 2026-07-06 feature allows a machine group's
   input chest to live in any location; off the farm, a client cannot read it.

Prefer **restricting these two pickers over adding standing sync**: when the menu opens, the client
requests a one-shot snapshot of available expansion locations and off-farm chests from the host. That
is one more message type on a channel the design already needs, not a new mechanism.

### Spawn location — `docs/architecture.md` is stale here

`docs/architecture.md` claims the worker spawns at "the farm entrance tile (resolved from
`farm.warps`)". That is only the **fallback**. `ResolveSpawnExitTile`
(`Dayswork/Orchestration/ShiftOrchestrator.Routing.cs:24`) first calls `FindHiringBuilding(farm)` and
uses `building.getPointForHumanDoor()`; the farm-warp heuristic runs only when no office exists.
AGENTS.md is correct. **Fix this doc as part of the work.**

This is good news for the 1:1 design: N offices give N distinct spawn and return points almost for
free, and it removes most of the need for the entrance-staggering and spawn-tile reservation that a
previous attempt required.

---

## Prior art you must know about

Multiple farmhands were **built (`376de8b`, 2026-07-08) and reverted (`f4116b5`, 2026-08-30)** after
playtesting — roughly 890 lines removed. **The reason for the revert was design, not defect: that
build allowed multiple farmhands from a *single* office, and it felt wrong — labor scaled by clicking
a menu row instead of by building anything.** The 1:1 office-to-worker rule in this brief is the
deliberate correction. Do not reintroduce a many-workers-per-office model.

Deleted then, and worth reconsidering on their merits (the problems they solved are inherent to N
workers, not to sharing an office):

- `WorkClaimRegistry` — stops two workers targeting the same tile.
- `ShoppingBudgetLedger` — stops two workers each spending the same gold at Pierre's. **Note this is
  a live bug even in single-player multi-office under a shared wallet.**
- Spawn-tile reservation and entrance stagger — largely obviated by per-office doors.
- Per-contract unique `FarmhandNpc.Name` and its `getTextureName()` override — **this is exactly the
  mechanism the worker-customization feature needs, so it returns.**
- The other-farmhands' crop-zone overlay in the zone-draw menu — relevant again, and more so in
  multiplayer where zones may overlap between *players*.

Deliberately kept from that era and still present: worker naming (`ContractPreferences.WorkerName`)
and the deposit chest-mutex wait in `DepositTripRunner`.

Retrieve the deleted implementations with `git show 376de8b` / `git show f4116b5` for reference, but
treat them as prior art rather than a baseline — they were written against a different topology and a
single-player world.

---

## Singleton assumptions that must be dismantled

The engine currently assumes exactly one worker. These are **correctness** blockers at N > 1, not
performance concerns, and they are the bulk of the work:

- `ShiftOrchestrator` holds a single nullable `_session`; `OnUpdateTicked` early-returns when it is
  null (`ShiftOrchestrator.cs:896`). All per-tick work reads `Session.*`.
- Services wired in `ModEntry` as long-lived but actually holding **per-worker** state:
  `_toolAnimator` (`SetWorker(farmhand)`), `_nav` / `WorkerMovementDriver` (`SetPacingProfile`),
  `_travel` (`Clear()` per shift), `_shopStockReader.ResetForShift()`,
  `CropHudNotifier.ResetForShift()`. Every one of these is called from `StartShift`, so a second
  worker starting its shift resets the first worker's state mid-shift.
- `HiringBuilding.WorkCompletedToday` is static mod state; it must become per-office (and it drives
  the evening lit-window/smoke overlay, which is now per-building).
- `HiringBuildingInteraction.FindHiringBuilding(farm)` returns the **first** matching building and has
  **8 call sites**. Converting this to "resolve the office for this contract by `Building.id`" is the
  spine of the refactor.
- `Contract` (`Dayswork.Core/Domain/Contract.cs`) has **no owner and no building reference**. It needs
  `OwnerId` (long) and the office's `Building.id` (Guid), with a persistence migration
  (`SaveDataSerializer` is currently v1→v2).
- `ContractStore` is a flat `Dictionary<ContractId, Contract>` with no ownership concept.
- AGENTS.md hard rule 3 ("Single active contract") and hard rule 6 ("Single-player only") are both
  superseded by this work and must be rewritten, not silently violated.
- `LocationPassabilityCache` is `readonly` on `ShiftSession`, so N workers build N identical grids of
  the same location. Sharing one per-day cache across sessions is an easy win — treat it as optional,
  and preserve the staleness/invalidation contract documented in `docs/game-data/pathing.md`.

---

## Constraints that still hold

All of AGENTS.md's hard rules except #3 and #6 remain in force, in particular:

1. **Core purity** — `Dayswork.Core/` references zero SMAPI/Stardew types. Ownership identity in Core
   must be a plain value (e.g. a `long` player id), never a `Farmer`.
2. **No Harmony.** SMAPI events only. This constrains the XP fix and the `Crop.harvest` leak — solve
   them within the existing `InvokeTaskActionGuarded` snapshot/restore discipline.
4. **Items are never lost and never degraded** — quantity, quality, and flavored/colored identity
   preserved through every hop. Multiplayer adds real concurrency pressure here: other players open
   chests, collect machines, and move things mid-shift. The chest → office output chest → shipping bin
   fallback chain must survive, now with a sponsor-aware shipping bin.
5. **The worker is despawned before save** — with N workers and clients that may sleep at different
   times, `CalendarHandlers.OnSavingHook` must clear *all* of them.
7. **Verify game content, never guess**, and record newly confirmed facts under `docs/game-data/`.

Also preserve: `net6.0`, `TreatWarningsAsErrors`, nullable enabled. Testing policy is unchanged —
tests are **required** for persistence formats and migrations, money math, and item-routing
invariants. Ownership/sponsor resolution and the contract-migration path both fall squarely in that
required set.

---

## Open questions to resolve in the plan

1. **Contract storage.** `Building.modData` versus host-held save data plus mod messages. Commit to
   one, with reasoning about save size, sync cost, demolition/move semantics, and what happens when an
   office is destroyed with an active contract.
2. **Authority boundary.** Precisely which SMAPI event handlers run host-only versus everywhere.
   Clients need building assets, worker visuals, i18n, and status UI, but must never run a shift loop,
   write save data, spend money, or mutate the world.
3. **Client editing — design this as "optimistic draft, authoritative commit."** A remote player must
   be able to hire and edit the contract for an office they own; host-only editing would leave a
   player able to build an office but not use it, which fails goal 2. The intended shape, given the
   sync findings above:

   - The client runs the **existing hiring flow unchanged** against its synced world copy, producing a
     `ContractDraft`. No new UI, no forked menu code.
   - On confirm, the client sends the draft to the host. The host re-runs `ContractTermsBuilder`
     against its authoritative world, re-checks affordability via `team.GetMoney(owner)`, re-resolves
     every `ChestRef` / machine / fish pond, then either commits and broadcasts, or rejects with a
     reason the client displays.
   - This is deliberately cheap because `ContractDraft` → `Contract` is already a pure Core
     transformation free of Stardew types (so it is already close to a serializable DTO, and
     persistence DTOs already exist), and `ContractTermsBuilder.BuildPreview` already re-runs on every
     draft mutation — running it host-side is the same code at a different call site, not new logic.
     The host's validation is the validation the confirm gate already performs.
   - Genuinely new work: a message envelope with request/response correlation, a rejection UX, and the
     one-shot host snapshot for the two unsynced pickers described above.

   Design the protocol and its failure modes (host rejects, host never replies, client disconnects
   between send and commit, two clients editing at once). Do **not** design continuous world-state
   sync to clients — it is far more expensive and buys little over this.

3a. **Ownership and access to the bulletin board.** Offices sit on the shared Farm, so any player can
   walk up to any office. `HiringBuildingInteraction` needs an ownership check and a defined
   experience for non-owners (read-only status, nothing, or an attribution line). Define behavior for
   an **orphaned office** whose `Building.owner` refers to a player who no longer plays. Note that
   **split-screen counts as multiplayer** and `Game1.player` differs per screen, so ownership checks
   must be per-screen, not per-process.

3b. **Storage and client UI are coupled — decide them together, not in separate phases.** If contracts
   live in `Building.modData`, every client has every contract for free, which makes read-only status
   UI nearly trivial and revives the deleted cross-farmhand zone overlay — now more valuable, because
   work zones can overlap between *players*, not just between one player's own workers. Weigh this as
   part of open question 1 rather than deciding storage on persistence grounds alone.
4. **Cross-player work conflicts.** Two players' offices whose work zones overlap; a worker depositing
   into a chest another player owns; two workers at the same shop counter.
5. **Migration.** A 1.x save has exactly one contract with no owner and no building link. Propose the
   adoption path — presumably bind it to the existing office and set the owner to the host — and the
   `SaveDataSerializer` version bump.
6. **Worker customization mechanism.** How per-worker color variation is achieved given
   `docs/game-data/farmhand-art.md`'s art contract and the fact that body animation is kept separate from
   tool/effect sprites. Whether it is a runtime tint or authored variant sheets, and how it
   round-trips through the netcode path above.
7. **Handshake behavior.** Exactly what happens when a modless or version-mismatched client connects:
   refuse to spawn workers, warn the host, kick, or something else.
8. **Sleep and day-end with N workers and multiple players**, including clients disconnecting
   mid-shift and the 8pm cap.

---

## Deliverable

A phased implementation plan following the repo's conventions in `docs/plans/` — problem, design,
decisions, and acceptance criteria per phase, with **status tracked only in `docs/plans/index.md`**,
never in the plan file itself.

Sequence the phases so each one is independently shippable and testable, and so single-player
regression is verifiable at every step. A defensible ordering is: per-worker state extraction and
N-office support in single-player first (the majority of the risk, and fully testable offline), then
ownership and the money/XP/shipping sponsor model, then the multiplayer authority layer and
handshake, then the cosmetic features. Justify your ordering if you disagree.

Include a smoke-test matrix. It must cover single-player regression, single-player with several
offices, host with no clients connected, a client joining and leaving mid-shift, shared **and**
separate wallets, a mid-save wallet-mode switch, an offline owner's worker running a full day,
save/load with several active contracts, and SVE expansion farms.

It must also cover the client-editing path specifically: a client hiring from its own office, a client
editing an existing contract, a host rejecting a client's draft (insufficient funds, a chest that
vanished between draft and commit), a client disconnecting between send and commit, a non-owner
interacting with someone else's office, and a client attempting to select an SVE expansion location or
an off-farm input chest. Include **split-screen** as its own row — it is multiplayer as far as
`Context.IsMultiplayer` is concerned, but every screen shares one process.

Flag anything you believe is infeasible or a bad idea rather than designing around it silently.
