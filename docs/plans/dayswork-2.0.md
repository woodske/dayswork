# Plan — Dayswork 2.0: N offices, ownership, multiplayer, cosmetics

**Brief:** [`docs/prompts/dayswork-2.0-prompt.md`](../prompts/dayswork-2.0-prompt.md) — its fixed
decisions are binding and are not re-argued here.
**Game-content references:** the brief's own verified section, plus
[`docs/game-data/multiplayer-and-ownership.md`](../game-data/multiplayer-and-ownership.md) (facts
verified 2026-09-07 while checking the brief's design against the decompile — several change the
design; see "Corrections to the brief").
**Prior art:** `git show 376de8b` (multi-farmhand build) / `git show f4116b5` (its revert);
[`docs/analysis/multiplayer-readiness.md`](../analysis/multiplayer-readiness.md) (facts sound,
recommendation superseded).
**Status:** tracked only in [`index.md`](index.md).

**Approved amendment (2026-09-08):** the user replaced the blanket no-Harmony constraint with
SMAPI events first, allowing narrow patches where they remove substantial complexity. The specific
exception is worker-tree drop attribution around `Tree.tickUpdate`, specified in
[review fix R6](dayswork-2.0-review-fixes.md#r6--prevent-cross-worker-capture-of-delayed-tree-drops).
Networking and XP retain their event/request and Sponsor paths. This amendment supersedes that
constraint in the original brief; it does not change the other fixed architecture decisions.

---

## Problem

Dayswork is one office, one worker, one player. 2.0 wants: a worker per office with no cap, offices
owned by whichever player built them, workers that keep working for offline owners while the host
plays, paintable offices, per-worker colour variation, and a deliberate rule for who gets the XP.
The engine assumes a singleton at almost every layer (`_session`, per-worker services wired as
long-lived singletons in `ModEntry`, `FindHiringBuilding` returning the first office, a flat
`ContractStore`, static `WorkCompletedToday`, `Game1.player` at 72 sites). Those singleton
assumptions are correctness blockers at N > 1 and are the bulk of the work; multiplayer is a layer on
top of a correct N-worker engine, not a separate feature.

## Goals, and what stays fixed

The brief's decisions stand: N identical offices, exactly one worker each, no cap; "office" never
"cabin"; all connected players must run Dayswork; sponsor = `Building.owner`; offline owners' workers
keep working (per-contract opt-in, default on); owner gains XP for all tasks with a toggle; vanilla
painting path; SMAPI-first integration with the narrow exception above; Core stays SMAPI-free;
items never lost or degraded; every worker despawned
before save; game content verified, never guessed. AGENTS.md hard rules **3** (single active
contract) and **6** (single-player only) are rewritten by this plan — Phase 1 and Phase 4
respectively — not silently violated.

## Corrections to the brief (verified against the decompile — see the game-data file)

These change the design and are called out first so nobody builds the brief's version.

1. **Do not inject sponsor identity through the fake action farmer.** The brief calls
   `Game1.player.CreateFakeEventFarmer()` "the seam where sponsor identity enters every guarded
   worker action". The seam is real, but `Farmer.IsLocalPlayer` is *identity*-based
   (`UniqueMultiplayerID == Game1.player.UniqueMultiplayerID`), and `CreateFakeEventFarmer` copies
   that id. Vanilla gates load-bearing behaviour on it: machine collection nulls `heldObject` and
   hands over the item **only when `who.IsLocalPlayer`** (`Object.cs:4626`), and tree XP/stats are
   computed only when the tool's last farmer is local (`Tree.cs:1498/1514`). Building the action
   farmer from a remote owner would flip that flag and break Manage Machines outright. **The action
   farmer keeps the host's identity forever.** Sponsor identity enters through four explicit
   channels instead: wallet (`team.GetMoney(owner)`), tools (`ToolLevelReader.ReadSnapshot(owner)`),
   shipping (`farm.getShippingBin(owner)` / `shipItem(item, owner)`), and XP routing (below).
2. **`gainExperience` already handles online guests; it silently drops XP for offline owners.** For
   a non-local farmer on the host it enqueues game message 17 on that farmer's `messageQueue`; only
   `Game1.otherFarmers` queues are ever flushed. So calling `owner.gainExperience` on the host is
   the whole delivery mechanism for a connected guest (their client applies it with the normal
   level-up UI) and no mod message is needed. For an owner who is not connected the call is a
   no-op. **Decision (2026-09-07): that is the intended behaviour — an owner who is not connected
   earns no XP from their worker.** Nothing is banked or persisted.
3. **XP is collected as a difference, not redirected.** Vanilla never asks "whose XP is this?" — it
   just adds to whichever farmer it has in hand. Two farmers end up holding worker XP after a beat:
   the fake action farmer (tree/rock XP, because vanilla credits the tool's last user) and the host
   (`Crop.harvest`, because it hardcodes `Game1.player`). The guard already snapshots and restores
   host state around every beat; it now also records the host's XP and skill levels before the beat
   and puts them back after, and reads what the fresh fake farmer accumulated. The two amounts are
   the XP the worker earned this beat; the mod then gives exactly that to the owner (or throws it
   away if the toggle is off). One code path for host-owned, guest-owned, and offline-owned workers.
4. **There is no vanilla "Paint Bucket".** Painting is Robin's carpenter menu → "Paint Buildings" →
   `BuildingPaintMenu`. And `Building.CanBePainted()` requires a **`Data/PaintData` entry** for the
   building type; a `_PaintMask` texture alone leaves the paint button refusing the office. Phase 5
   ships both assets plus the region strings.
5. **Money call sites:** 14 lines across 7 files (reads included), not ~8. All go through one
   wallet accessor.
6. **"Cabin" is already in the codebase and the UI.** `CabinChestService`, the building's display
   name "Farmhand Cabin", both porch-chest names, the automatic-output labels, and two overflow
   notices say "cabin". Renaming them is Phase 0 and is player-visible (changelog entry).
7. **`Building.owner` is set by `buildStructure`, not the constructor.** Every carpenter-built
   office has its builder's id; anything placed by a path that bypasses `buildStructure` has `0`.
   Migration treats `0` as host-owned.
8. **Handshake ordering has a hard limit.** `PeerContextReceived` fires before the game approves the
   connection, so for SMAPI clients the host can despawn every worker before the world snapshot
   goes out. For a *vanilla* (no-SMAPI) client SMAPI records a "vanilla player" context on
   `PlayerIntro`; whether that raises `PeerContextReceived` early enough is not documented. If it
   does not, a vanilla guest joining while a worker is live will still hit the netcode throw on
   their side. Phase 4 verifies; if unavoidable it is documented as the one degradation the mod
   cannot prevent, and `KickIncompatiblePeers` becomes the recommended host setting for public
   lobbies.

---

## Architecture decisions

### D1. Contract storage: durable data in `Building.modData`, host-written, everyone-read

Each office holds its own contract as one JSON string under
`modData["Bindicle.Dayswork/Contract"]` (the existing DTO serializer, schema v4). Per-day volatile
state that other players should *see* also lives in modData, written on change, never per tick:
`…/DoneOn` (day stamp, drives the evening overlay), and `…/Live` (a shift is running).
`…/Revision` was **not** given a key of its own (changed while building Phase 1): the revision is a
field on the serialized contract, so anyone who can read the contract already has it and a second
key would be a copy that can disagree with the first. `…/Live` is deferred to Phase 4, which is the
first phase with a client that needs it.
Per-worker cosmetic/status state lives in the NPC's `Character.modData` (`…/Name`, `…/Variant`,
`…/Stamina` throttled to ~5 % steps).

Why, against host-held save data + read-only views over mod messages:

| | `Building.modData` | Host save data + messages |
|---|---|---|
| Identity / demolish / move | Free — the contract *is* the building's; demolish removes it, move keeps it | Parallel bookkeeping keyed by `Building.id`, reconciled on every `BuildingListChanged` |
| Client visibility | Every client has every contract at all times (read-only status UI and the cross-owner zone overlay need no channel) | A snapshot/refresh protocol, plus staleness handling |
| Save size / sync cost | One string per office, a few KB; written on edit only (a net delta per write) | Same size; no sync cost but no visibility either |
| Failure surface | A stale/garbled string affects one office; the serializer already skips malformed contracts | One blob; same serializer |
| Authority | Only the host writes (clients send requests). Nothing enforces that at the netcode level, so it is a code discipline: every write goes through one host-only `OfficeContractStore` | Enforced by SMAPI (`WriteSaveData` is main-player-only) |

The one thing modData cannot hold is data that is *not* per-building: per-owner farmhand upgrades
(D9) stay in host save data.

**Office destroyed with an active contract:** `BuildingListChanged` (host) → if that office has a
live shift, end it through the normal early-stop path (deposit → its output chest is gone → shipping
bin of the owner; hard rule 4 holds) → the contract is gone with the building. **No refund**: an
unexecuted one-time contract's price is forfeited (decided 2026-09-07 — demolition is
player-initiated and a refund path is more code than the case deserves); the owner gets a notice.

### D2. Authority boundary

| Runs **host-only** (`Context.IsMainPlayer`) | Runs **everywhere** |
|---|---|
| Day-start scheduler; every `ShiftOrchestrator` event (tick, time, world-list changes); `Saving` sleep-settle; contract/upgrade persistence adapters; all modData writes; money and XP routing; office chest ensure (`OfficeChestService`); handshake host side; request handling (commit / action / snapshot); demolition handling | `AssetRequested` (building data, textures, paint mask, PaintData, NPC sheets, strings); i18n; GMCM; bulletin-board interaction (ownership check, then either the local commit path or a request); hiring menus (unchanged, operating on the client's synced world); evening overlay (reads `…/DoneOn`); `FarmhandNpc` client draw (name/stamina/variant from modData); request/response client side; the "paused: incompatible peer" banner |

Split-screen: the engine handlers are gated on `Context.IsMainPlayer`, which is true only on screen
0, so the shift loop runs once per process. Everything per-screen (open draft, pending request,
ownership checks via `Game1.player`) uses SMAPI `PerScreen<T>`; the second screen is simply a client
that happens to share the process and its mod messages loop back locally.

`MultiplayerGuard` becomes `Authority` with `IsHost`, `IsRemoteClient`, `IsSplitScreenGuest`; the
two current `IsMultiplayer()` early-returns are deleted.

### D3. Client editing — "optimistic draft, authoritative commit"

The client runs the existing hiring flow unchanged against its synced world and produces a
`ContractDraft`. On confirm (and on Pause/Resume/Cancel/buy-upgrade) it sends a request; the host
validates against *its* world and either commits + writes modData or rejects with a reason.

Message set (all `Helper.Multiplayer.SendMessage`, targeted by mod id and player id):

| Type | Direction | Payload |
|---|---|---|
| `Hello` / `HelloAck` | host ↔ peer on connect | mod version, `ProtocolVersion` |
| `MenuSnapshotRequest` / `MenuSnapshotResponse` | client → host → client, on hub open | office id → expansion locations the client may select, off-farm input chests (as `ChestRef` + display name), the requester's upgrade state |
| `ContractCommitRequest` / `ContractCommitResponse` | client → host → client | request id, office id, `ContractDtoV3`, expected revision → `Accepted(revision)` or `Rejected(code, detail)` |
| `ContractActionRequest` / `ContractActionResponse` | client → host → client | request id, office id, Pause / Resume / Cancel / PurchaseUpgrade(kind) |
| `OwnerNotice` | host → owner | i18n key + args (the notices `ShiftOutcomeDispatcher` shows today, delivered to the owner instead of the host) |
| `DaysworkPaused` / `DaysworkResumed` | host → all | reason, offending player name |

Host-side validation is the confirm gate the local flow already performs, at a different call site:
`ContractTermsBuilder.BuildPreview` on the received draft; affordability via
`team.GetMoney(owner).Value` for a one-time contract; every `ChestRef`, machine, and fish pond
re-resolved in the host's world; office still exists, `Building.owner` matches `FromPlayerID` (or
sender is host); expected revision equals stored revision. Rejection codes:
`CannotAfford`, `ChestMissing`, `MachineMissing`, `PondMissing`, `InvalidScope`, `NotOwner`,
`OfficeGone`, `Stale`, `VersionMismatch`, `Paused`. Requests are processed synchronously inside
`ModMessageReceived` (no interleaving); a bounded set of recent request ids makes a retried commit
idempotent (a one-time price is charged once).

Failure modes:

- **Host rejects** → client shows the reason (HUD + a line on the summary page) and re-opens the hub
  with the draft intact so the player can fix it.
- **Host never replies** → client-side timeout (10 s wall clock) → "The host did not respond; your
  changes were not saved" → draft kept, hub re-opened. The host may still have committed; the next
  hub open reads modData and shows the truth.
- **Client disconnects between send and commit** → host commits if the request arrived (it is the
  owner's contract; they see it on rejoin), else nothing happened.
- **Two editors at once** → impossible for the *same* contract by owner rule, except host-admin vs
  owner; the revision check rejects the loser with `Stale`.
- **Host's own flow** → the same commit function, called directly ("loopback"), so single-player
  and host paths are one code path.

The two unsynced pickers (SVE expansion locations; off-farm machine input chests) are **restricted,
not synced**: on a client, those pickers show only what the `MenuSnapshotResponse` listed (empty and
labelled "waiting for host…" until it arrives); the host still re-validates at commit.

### D4. Ownership and the bulletin board

Any player can walk up to any office. Interaction resolves the *clicked* building (not the first
office) and branches on `building.owner` vs `Game1.player.UniqueMultiplayerID` (per-screen):

- **Owner** → existing hire/manage flow (local commit on host, request on client).
- **Host, not owner** → read-only card (owner name, worker name, status, schedule) with admin
  actions **Pause / Cancel** only (never edit scope), so an absent owner's worker can be stopped.
- **Other non-owner** → the same read-only card, no actions.
- **Orphaned office** (`Game1.GetPlayer(owner)` is null — farmhand slot deleted, or `owner == 0` in a
  co-op save): the host's card gains **Claim office** (sets `Building.owner` to the host, cancels the
  contract). Day-start skips orphaned offices with a host notice; nothing is charged.

### D5. Cross-player work conflicts

- **Overlapping zones:** `WorkClaimRegistry` (Core, revived from `376de8b`) on the per-day fleet
  state; first shift to reach a batch claims each `(location, tile, task)` / animal / machine / pond
  / managed-dirt key; claims are never released within the day. Plus the informational overlay in the
  zone-draw menu showing **other contracts'** zones (all contracts are in modData, so this works on
  clients too) — now coloured by *owner*, since overlap between players is the interesting case.
- **Depositing into a chest another player placed:** allowed; vanilla chests have no owner and the
  contract owner chose it. The existing mutex wait/overflow handles a chest someone has open.
- **Two workers at a shop counter:** `ShoppingBudgetLedger` (Core, revived) keyed by **wallet
  identity** — one ledger under a shared wallet (this is the live single-player multi-office bug the
  brief flagged), one per owner under separate wallets — so two workers never plan to spend the same
  gold. Physical co-location at the counter is a smoke item (workers may block each other's tiles).

### D6. Migration (1.x → 2.0)

`SaveDataSerializer` goes v3 → **v4**. `ContractDtoV3` adds `OwnerId` (long), `OfficeId` (Guid
string), `Revision` (int), and preferences `RunWhileOwnerOffline` (default true),
`GrantExperience` (default true), `Appearance` (variant string, default ""). The `Contract` record
gains `OwnerId`/`OfficeId`; the five back-compat constructor overloads collapse into one (all
callers are in-repo; tests use `ContractStructuralComparer`).

Adoption on the host's first `SaveLoaded` under 2.0 (pure Core function, unit-tested):

1. Read `Dayswork.Contracts` (any schema ≤ 3).
2. Take the primary open contract (Active over Paused; the residual-cancel rule from the reverted
   build still applies to *unbound* contracts). If exactly one office exists, bind it there:
   `OwnerId = building.owner != 0 ? building.owner : MasterPlayer.UniqueMultiplayerID`,
   `OfficeId = building.id`. Write it to that office's modData.
3. If no office exists (demolished after hiring) the contract cannot run and is dropped with a
   notice; no refund (same rule as D1). Zero offices with a bound contract is otherwise impossible
   in 1.x (`OnePerFarmBuildCondition`).
4. Overwrite `Dayswork.Contracts` with a v4 marker envelope (`SchemaVersion: 4`, no contracts,
   `MigratedToBuildingModData: true`). A 1.x mod opening that save sees "newer schema", loads
   nothing, and cannot double-bill — today's behaviour, so nothing new to test there.
5. `Dayswork.FarmhandUpgrades` v1 → v2: the existing global state is keyed to the host's id (D9).

### D7. Worker appearance: authored mask + the game's own painter, synced by texture name

"Colour variation is sufficient", so reuse exactly the mechanism painting uses. Author
`assets/farmhand_PaintMask.png` (same grid as `farmhand.png`; pure red / lime / blue mark up to
three recolourable regions — e.g. shirt, trousers, hat) and generate each variant at runtime with
`BuildingPainter.Apply(baseSheet, "Characters/DaysworkFarmhand_PaintMask", colour)`, where `colour`
is a `BuildingPaintColor` filled from the variant's three HSL triples. Each variant is registered as
its own asset `Characters/DaysworkFarmhand_<variantKey>` via `AssetRequested` (a `LoadFrom` that
builds the texture), so:

- the NPC's `Sprite.textureName` is a synced `NetString` → every client loads the same
  deterministic asset with no custom sync;
- `getTextureName()` is overridden to return the variant asset name read from
  `modData["…/Variant"]`, so `reloadSprite`/`ChooseAppearance` cannot revert it;
- body animation stays separate from tool/effect overlays exactly as `farmhand-art.md` requires —
  only the body sheet is recoloured; tool overlays are untouched.

UI: an **Appearance** row on the Preferences spoke cycling through a curated preset list (8–12
palettes) with a live sprite preview; the variant key is a compact string in
`ContractPreferences.Appearance`. Free HSL sliders (reusing `BuildingPaintMenu.ColorSliderPanel`)
are a later nicety, not part of 2.0. Portrait: shared placeholder for now (the art contract is
unchanged).

### D8. Handshake and degradation

- `ProtocolVersion` constant (int) in `Dayswork`; bumped whenever message DTOs or modData formats
  change. Compatible ⇔ equal protocol version (patch releases may differ).
- **Host, on `PeerContextReceived`** (SMAPI peers): `peer.GetMod("Bindicle.Dayswork")` missing →
  `NoMod`; present with a different `ProtocolVersion` (sent in `Hello`) → `Version`. Vanilla peers
  are caught at `PeerConnected` with `HasSmapi == false`.
- **Default policy: suspend.** On detecting an incompatible peer the host (1) ends every live shift
  through the early-stop path — deposit, overflow, despawn — *before* the connection is approved
  where the event ordering allows it, (2) refuses to spawn workers while any incompatible peer is
  connected, (3) shows a persistent host HUD warning and a chat line everyone (including modless
  guests) can read, (4) resumes automatically on the next day start after they leave.
- **Config option `KickIncompatiblePeers`** (default off): `Game1.server.kick(peerId)` instead,
  with the same chat line. Recommended for public lobbies because of correction 8.
- **Client side:** if the host has no Dayswork / mismatched protocol, all Dayswork UI on that
  client shows a single explanatory card; nothing else runs.
- Version-mismatched *peers with the mod* are still safe at the netcode level (the type resolves),
  so the only thing refused for them is the request protocol (`VersionMismatch` responses).

### D9. Farmhand upgrades: per owner, host save data, delivered in the menu snapshot

Today's three upgrades (Speed, Speed2, Energy) are global. With multiple owners that is unfair in
co-op and, per office, would make single-player players re-buy them per building. **Per owner**
keeps single-player exactly as it is (one owner) and is fair in co-op: `FarmhandUpgradeStore`
becomes a dictionary keyed by owner id (save-data v2, host-only), the purchase is a host action
(`PurchaseUpgrade` request, charged to the buyer's wallet), and a client learns its own state from
the `MenuSnapshotResponse`. (Decided 2026-09-07.)

### D10. Sleep, day end, disconnects

- The day ends when the **host's** `Saving` fires; `ShiftFleet.StopForSleepAndSettle` stops every
  live shift and despawns every worker before persistence (hard rule 5, N-safe). A client going to
  bed early changes nothing for workers.
- 8 pm cap is per shift, unchanged.
- **Owner disconnects mid-shift:** if the contract's `RunWhileOwnerOffline` is true (default) the
  worker keeps going with the tool snapshot taken at shift start; otherwise the host ends that shift
  early on `PeerDisconnected`. Day-start for an offline owner reads tools from the `farmhandData`
  `Farmer` (frozen at last disconnect — accepted by the brief).
- **Host exits to title:** `ReturnedToTitle` resets the fleet as today.
- **XP while the owner is offline is not granted** (corrections 2/3). The flush calls
  `owner.gainExperience` only when the owner is connected; otherwise the amount is discarded.

### D11. Optional: one passability cache per day

`LocationPassabilityCache` moves from `ShiftSession` to the per-day fleet state and is shared by all
sessions; the four `World.*ListChanged` handlers invalidate it once instead of once per shift; the
worker-clear-site invalidation and the stuck-recovery `InvalidateLocation` keep their exact
semantics. Nothing about the staleness contract in `docs/game-data/pathing.md` changes (a stale
answer still only degrades routing quality). Optional; do it in Phase 1 only if two workers on a
large farm show a visible hitch at shift start.

---

## Phasing and order

The brief's order is kept for the risky spine — engine first, then sponsor model, then multiplayer —
with **one deliberate change: worker appearance moves ahead of multiplayer** (Phase 3). Reason: the
moment Phase 1 ships, a single-player farm can have three identical workers, and telling them apart
is a usability need, not a cosmetic. The mechanism (synced `textureName` + modData) is multiplayer-
safe by construction and costs nothing to move; only its netcode round trip waits for Phase 4's
smoke pass. Painting stays last: it has no coupling to anything.

Every phase builds clean, keeps all tests green, and is play-testable in single-player.

| # | Phase | Ships on its own? | Rewrites |
|---|---|---|---|
| 0 | Naming, docs, verified facts | yes | — |
| 1 | N offices, N workers, per-office contracts (single-player) | yes | hard rule 3 |
| 2 | Sponsor model: wallet, tools, shipping, XP, offline flag | yes (owner == host) | — |
| 3 | Worker appearance | yes | — |
| 4 | Multiplayer authority, handshake, client editing | yes | hard rule 6 |
| 5 | Paintable offices | yes | — |

---

## Phase 0 — Naming, docs, verified facts

**Problem.** The word "cabin" is in a type name, the building's display name, chest names, labels,
and notices; `docs/architecture.md` describes the spawn point wrongly; the facts verified for this
plan need a home.

**Design / work.**
- Rename `CabinChestService` → `OfficeChestService` (and its test); i18n: `building.office.name` →
  "Farmhand Office", both chest names, `ui.manage_crops.output_automatic`,
  `ui.manage_machines.output_automatic`, `ui.zone_chest.*automatic*`, the two
  `notify.items_deposited_chest*` strings, the `ShiftOutcomeDispatcher` trace text.
- `docs/architecture.md`: spawn/return is the office human door; farm-warp heuristic is the
  no-office fallback. Fix the "single-player" sentences to point at this plan.
- `docs/game-data/multiplayer-and-ownership.md` (written with this plan) indexed; AGENTS.md
  "Verified game-content references" gets a bullet for the `IsLocalPlayer` finding.
- CHANGELOG `Unreleased → Changed`: the building is now called the Farmhand Office.

**Acceptance.** `grep -rni cabin Dayswork Dayswork.Tests` returns only the Log-Cabin footprint
comment in `HiringBuilding.cs`; build + tests green; architecture doc matches
`ResolveSpawnExitTile`.

## Phase 1 — N offices, N workers, per-office contracts (single-player)

**Problem.** Every singleton listed in the brief's "Singleton assumptions" section. This is the
majority of the risk and is fully testable offline.

**Design.**
- **Contract identity.** `Contract` gains `OwnerId` + `OfficeId`; `ContractStore` becomes
  `OfficeContractStore`: a map `OfficeId → Contract` hydrated from every office's modData on
  `SaveLoaded`, written back to *that* office's modData on every mutation (never per tick). The
  `GetPrimaryOpen` / `GetScheduledForDate` singletons become `ForOffice(id)` /
  `ScheduledForDate(date)` returning a list. `CancelResidualOpenContracts` is replaced by the
  adoption rule (D6). Migration per D6, with the v4 serializer.
- **Office resolution.** `HiringBuildingInteraction.FindHiringBuilding(farm)` (8 call sites) →
  `OfficeResolver.TryGet(Guid officeId)` / `EnumerateOffices(farm)` / `AtTile(farm, x, y)`. The
  interaction resolves the clicked building; the orchestrator resolves *its contract's* office for
  spawn door, return door, idle-wait facing, input chest, output chest. `HiringBuilding.TryGet*Chest`
  take a `Building`. `OnePerFarmBuildCondition` is removed from `BuildData()`.
- **Fleet.** Revive `ShiftFleet` + `FleetDay` from `376de8b`, adapted: one `ShiftOrchestrator` per
  live shift with its own `ToolSwapAnimator`, `WorkerMovementDriver`, `TravelRunner`, and
  `ShopStockReader`; shared stateless services as today. `FleetDay` holds `WorkClaimRegistry`,
  `ShoppingBudgetLedger` (keyed by wallet identity), the day's `CropHudNotifier` reset, and
  optionally the shared passability cache (D11). Spawn-tile reservation and entrance stagger are
  **not** revived — each office has its own door; `ResolvePassableNearby` already disambiguates two
  adjacent offices. Fan-out stays sequential (that sequencing *is* the concurrency model for the
  `Game1.player` snapshot/restore).
- **Per-office day state.** `HiringBuilding.WorkCompletedToday` → `modData["…/DoneOn"]` written by
  `HandleExit`, read by the overlay renderer per building (now iterating all offices), cleared by
  day-start.
- **Worker identity.** `FarmhandNpc.Name = "DaysworkFarmhand_" + officeId:N`, `getTextureName()`
  override pinned to the shared sheet (from `376de8b`; Phase 3 makes it variant-aware).
- **UI.** `OpenFromBuilding(building)`; `ContractMenu` shows *this office's* contract;
  `OpenHiringFlow` guard becomes "this office already has a contract"; the zone-draw overlay shows
  other contracts' zones (revived `OtherWorkerZones`, now from the store). `CropHudNotifier` dedup
  is day-scoped (a second shift's start must not re-arm the first's flags).
- **Scheduler.** Iterates every Active contract due today, resolving each office; an office that no
  longer exists cancels its contract with a notice; failures are per contract (one bad contract
  cannot stop the others).
- **Demolition.** `BuildingListChanged` removal of an office → end its shift early; contract and
  any unexecuted one-time price are forfeited per D1, with a notice.
- **Sleep / save / title.** Fleet-wide stop, despawn, reset.
- **AGENTS.md hard rule 3** rewritten: "one contract per office; at most one live shift per office;
  N offices ⇒ N contracts."

**Decisions.** Contracts in modData from day one (D1) so Phase 4 gets client visibility free;
`WorkClaimRegistry` and `ShoppingBudgetLedger` return as Core types; spawn stagger does not return.

**Changed while building (2026-09-07):**
- `…/Revision` is a field on the stored contract, not a modData key of its own (see D1).
- `…/Live` is deferred to Phase 4; nothing in single-player reads it.
- The v4 DTO carries the Phase-2/3 owner preferences (`RunWhileOwnerOffline`, `GrantExperience`,
  `Appearance`) already, so those phases are behaviour-only and there is no second schema bump.
- `ChestResolver`'s three `ShouldExcludeSelectableFarmChest` overloads collapsed into one
  `ReservedOfficeChestTiles(farm)` set — "the office's two tiles" became "every office's tiles",
  and a tile set says that more plainly than six nullable coordinates.
- Adoption gained a third outcome, `DroppedAmbiguous`: a save from the reverted multi-farmhand
  build can have several offices *and* a legacy contract, and which office it belonged to is not
  recoverable. It is dropped with a notice rather than bound to an arbitrary office.

**Tests (required).** v4 round trip + v3→v4 upgrade + malformed-skip; the pure adoption/binding
function (one office / zero offices / owner 0 / residual extras); `OfficeContractStore` invariants
(one contract per office, revision increments, hydrate from N offices); `WorkClaimRegistry`;
`ShoppingBudgetLedger` keyed by wallet.

**Acceptance.** Built 2026-09-07; every item below is code-complete with unit tests where the
plan asked for them, and every one marked *(smoke)* still owes an in-game pass.

- [x] *(smoke)* Two or three offices, each with its own contract, run concurrent shifts; each worker
  spawns at and returns to its own door; deposits and overflow go to its own office chest; each
  office lights independently.
- [x] *(smoke)* Overlapping zones between two contracts are serviced once (claims), and the
  zone-draw menu shows the other contract's zones.
- [x] *(smoke)* Under one wallet, two managed-crop shopping trips never over-spend (ledger).
- [x] *(smoke)* Save/load with several active contracts round-trips through modData; a 1.x save
  adopts its contract onto its office with the host as owner; the old save key holds the v4 marker.
- [x] *(smoke)* Demolishing an office with a live worker ends the shift safely (items to shipping
  bin), shows the forfeit notice, and leaves no contract behind.
- [ ] *(smoke)* Single-player regression: everything in the "SP regression" matrix row.

## Phase 2 — Sponsor model: wallet, tools, shipping, XP, offline flag

**Problem.** `Game1.player` is used for money (14 sites), tools, shipping, HUD notices, and — by
accident — XP. In single-player owner == host so this phase is behaviour-preserving except for XP,
which becomes deliberate.

**Design.**
- `Sponsor` (in `Dayswork/`, game-side): `Farmer? Resolve(long ownerId)` = `Game1.GetPlayer(id)`
  (online or offline; never `getFarmer`, which falls back to the host); `NetIntDelta Wallet(owner)`
  = `Game1.player.team.GetMoney(owner)`; `IInventory ShippingBin(farm, owner)`.
- All money reads/writes go through `Wallet(owner).Value`. `ShopPurchaseService` takes the wallet,
  not a `Farmer`. `RecurringContractScheduler` evaluates and charges per contract owner.
  `HiringFlowCoordinator.ConfirmContract` charges the owner (host path). Festival refunds credit
  the owner. `UpgradesMenu` affordability reads the buyer's wallet.
- Tools: `ToolLevelReader.ReadSnapshot(Sponsor.Resolve(contract.OwnerId))` at shift start.
- Shipping: `DepositTripRunner` and the overflow dispatcher use `getShippingBin(owner)` /
  `shipItem(item, owner)`; the office chest → owner's bin fallback chain is preserved.
- **XP.** In `RunGuardedWorkerBeat`: snapshot `Game1.player.experiencePoints[0..5]`, the five skill
  `NetInt`s, `newLevels.Count`, `Game1.stats["MasteryExp"]`; after the body, compute the host delta
  per skill, restore all of it (truncate `newLevels`; the HUD trim already removes the level-up
  textbox); read the fake farmer's `experiencePoints` (fresh per beat ⇒ delta); credit both to a
  per-session `XpLedger` (Core, pure). Flush at batch boundaries and shift end via
  `Sponsor.GrantExperience(ownerId, skill, amount)`: toggle off → drop; owner connected →
  `farmer.gainExperience` (applied locally for the host; the game's own type-17 message for
  guests); owner not connected → drop. Fieldwork XP now reaches the owner (it never did before) —
  changelog entry.
- Preferences: `GrantExperience` (default true) and `RunWhileOwnerOffline` (default true, inert
  until Phase 4) on the Preferences spoke.
- Notices: `ShiftOutcomeDispatcher` methods take the owner id; in this phase they still show a
  local HUD when the owner is `Game1.player` and log otherwise (Phase 4 adds delivery).
- Action farmer: **unchanged** (host identity) — see correction 1; a comment on
  `CreateWorkerActionFarmer` says why.

**Decisions.** XP is a delta harvested per beat, granted in batches, and dropped when the owner is
not connected; the XP toggle is a **per-contract preference** (it travels with the draft, persists,
and is "the owner's toggle" without a config-sync channel).

**Changed while building (2026-09-07):**
- `Sponsor` gained `ResolveOrHost`. Owner resolution deliberately splits in two: `Resolve` returns
  null for an unknown id (a deleted farmhand slot), while wallet, shipping and tools fall back to
  the host rather than taking a shift down over a missing owner. XP still uses the strict lookup —
  a missing owner earns nothing.
- The XP toggle is honoured at **harvest** time, not at flush: with it off, the host's experience is
  still restored (that is the point of the toggle) but nothing is banked, so the ledger never holds
  XP that can't be granted.
- The batch-boundary flush is a **batch-index change detected in the tick loop**, not a call at each
  of the 27 `CurrentBatchIndex++` sites.
- `ShiftOutcomeDispatcher`'s per-contract notices keep taking the `Contract` (which carries
  `OwnerId`); only `ShowContractLostNotice` and `DispatchOverflowDelivery`, which have no contract
  in hand, gained an explicit `ownerId`.
- The source-lint test covers the **shipping** seam as well as money — `getShippingBin`/`shipItem`
  outside `Sponsor` are the same class of mistake, and the check costs one more regex.
- Farmhand upgrades stay **global** (still `Game1.player`'s purchase against their own wallet).
  Per-owner upgrade state is D9, which the phasing table puts in Phase 4.
- `ShiftSession.WalletId` was a Phase-1 placeholder returning `OwnerId`; it now asks
  `Sponsor.WalletId`, which collapses every owner onto one id when `useSeparateWallets` is false.
  Without that, two owners under co-op's shared money would have got a `ShoppingBudgetLedger` each
  and could both plan to spend the same gold — the exact bug D5 asks the ledger to prevent.

**Tests (required).** `XpLedger` accumulate/flush;
wallet accessor used for every charge/refund path (a test double for `NetIntDelta` is not possible
in Core — test the Core decision engines with owner money as input, and add a source-lint test that
no `Game1.player.Money` remains in `Dayswork/`).

**Acceptance.** Built 2026-09-07; code-complete with the unit tests the plan asked for. Every item
marked *(smoke)* still owes an in-game pass (deferred with Phase 1's, per the 2.0 smoke matrix).

- [x] Wallet: no `Game1.player.Money` (nor any `Farmer.Money`) anywhere in `Dayswork/` — enforced by
  `SponsorSeamLintTests`; every charge, refund and affordability read goes through `Sponsor`.
- [x] Shipping: no `getShippingBin`/`shipItem` outside `Sponsor` — same lint.
- [x] Tools: the shift snapshots `Sponsor.ResolveOrHost(contract.OwnerId)`'s tool levels.
- [x] XP: harvested as a host + action-farmer delta per guarded beat, banked in the per-shift
  `XpLedger`, flushed at batch boundaries and shift end through `Sponsor.GrantExperience`.
- [x] Action farmer unchanged (host identity), with the comment saying why (correction 1).
- [x] `GrantExperience` and `RunWhileOwnerOffline` on the Preferences spoke.
- [ ] *(smoke)* Single-player regression unchanged for money and items.
- [ ] *(smoke)* With the toggle on, one harvest beat grants the same farming XP as before and
  fieldwork now grants foraging/mining XP.
- [ ] *(smoke)* With the toggle off, no skill changes during a shift (skills page before/after).
- [ ] *(smoke)* Machine collect still works (guards correction 1).

## Phase 3 — Worker appearance

**Problem.** N workers look identical.

**Design.** D7. Assets: `farmhand_PaintMask.png`; a preset table (Core, pure: variant key → three
HSL triples + display name). `AssetRequested` serves `Characters/DaysworkFarmhand_<key>` by
loading the base sheet and applying `BuildingPainter.Apply` with the preset (falls back to the base
sheet if the mask fails to load). `FarmhandNpc`: `getTextureName()` returns the variant asset;
`displayName` getter reads `modData["…/Name"]`; spawn writes `…/Name`, `…/Variant`, initial
`…/Stamina`; the parameterless constructor is made valid and inert (no content loads, no
`Portrait` in the ctor — lazily in draw). Preferences spoke: Appearance cycle with preview.
`ContractPreferences.Appearance` persists the key.

**Acceptance.** Two offices' workers render with different palettes; reloading a save keeps each
worker's palette; the preview matches the in-world sprite; tool/effect overlays are unaffected;
`FarmhandNpc()` constructs without touching `Game1.content`.

**As built (2026-09-07)** — four details settled differently from the sketch above, none of them a
change of approach:

- **The default palette is the plain base sheet**, not a painted variant of itself. `denim` is a real
  key in the preset list (so the cycle has a name for it and `""` resolves to it), but
  `TextureNameFor` returns the bare `DaysworkFarmhand` for it — vanilla's own painter refuses a
  no-op recolour anyway (`RequiresRecolor()`), so painting the default would have meant a null
  return and a needless texture copy every time.
- **The portrait is not loaded in the constructor at all**, rather than loaded lazily in draw.
  Dropping the eager `Game1.content.Load` leaves vanilla's own lazy `Portrait` getter to resolve
  `Portraits/DaysworkFarmhand_<key>`, which `FarmhandAppearance` serves (placeholder for every
  variant — only the body sheet is recoloured). Nothing reads the worker's portrait today.
- **`displayName`'s setter is a no-op.** The getter reads `modData`, as designed; the setter has to
  swallow writes because `reloadSprite` assigns `translateName()` — which for this NPC is the unique
  per-office internal name — and that would clobber the player's chosen name.
- **Stamina lives only in `modData`**, read back in `drawAboveAlwaysFrontLayer`, instead of being
  mirrored in private fields — one source of truth rather than a second copy no remote client could
  see. It is quantised to 5 % of capacity on write, per D1: `SetStamina` runs once per work beat, and
  the bar is 40 px wide, so the exact figure would be hundreds of net updates a day for changes
  nobody can see. Empty and full are never rounded away.

Sprite asset names use forward slashes throughout so that the name vanilla computes in
`ChooseAppearance` (`"Characters/" + getTextureName()`) matches `Sprite.textureName` exactly, which
makes `TryLoadSprites` an early return instead of a mid-shift sprite reset.

## Phase 4 — Multiplayer authority, handshake, client editing

**Problem.** Everything runs on every peer; there is no protocol; the NPC is host-only in spirit.

**Design.** D2, D3, D4, D8, D9, D10.
- `Authority` replaces `MultiplayerGuard`; `ModEntry` registers the host-only group under
  `if (Context.IsMainPlayer)`-checks inside each handler (not at registration — `IsMainPlayer` can
  only be evaluated once a save is loaded).
- `PerScreen<T>` for the hiring coordinator's transient state, the pending request, and the
  snapshot cache.
- `ContractRequestHandler` (host) implements commit/action/snapshot; `ContractRequestClient`
  sends and correlates; both share `MessageDtos` (Core-serializable, Newtonsoft as today).
- `Handshake` (host + client sides) per D8, with `KickIncompatiblePeers` in config/GMCM and the
  paused banner.
- `FarmhandNpc` client inertness verified: no `controller`, no local movement; stamina bar from
  modData; `update` override no-op on non-host if the vanilla gate proves insufficient.
- Offline owners: day-start tool snapshot from `farmhandData`; `RunWhileOwnerOffline == false` →
  early stop on `PeerDisconnected`; XP flushes while they are offline are discarded; orphan handling
  per D4.
- Notices via `OwnerNotice` when the owner is a guest.
- Upgrades per owner (D9): store v2, purchase request, snapshot delivery.
- AGENTS.md hard rule 6 rewritten: "host-authoritative multiplayer; all peers must run the same
  Dayswork protocol version; clients never run a shift loop, write save data, spend money, or
  mutate the world — they send requests."

**Tests (required).** Message DTO round trips; the pure commit validator (draft + world snapshot →
accept/reject code) for every rejection code; request-id dedupe; revision/stale logic; per-owner
upgrade store v1→v2.

### What was actually built (2026-09-07) — corrections to this phase as written

1. **The loopback test is `Context.IsOnHostComputer`, not "am I the host".** A split-screen guest
   shares the host's process, so its request is handed to `ContractRequestHandler` directly rather
   than sent as a message — which is both simpler and more reliable than trusting a mod message to
   loop back between local screens. `Authority.HostIsInThisProcess` wraps it, and it is *also* the
   right guard for the office-modData write-back: the write a split-screen guest triggers is the
   host's write. `IsSplitScreen` would have been the wrong test — SMAPI only sets it on the host's
   computer, so split-screen players on a *guest* machine are plain remote clients (verified;
   recorded in `docs/game-data/multiplayer-and-ownership.md`).
2. **The upgrade store goes to schema v3, not v2.** It was already at v2 — the second speed
   upgrade added that — so per-owner keying is v3. A v1 or v2 payload migrates onto the host's id,
   which is the only player who could have bought those upgrades.
3. **`KickIncompatiblePeers` is not in `ConfigSnapshot`.** It is a host networking policy that no
   part of the shift engine reads, so it lives on `ModConfig` only; adding it to the Core snapshot
   would have rippled through `ConfigSnapshotFactory`, `ConfigDefaults`, `FarmhandUpgradeEffects`,
   and their tests for nothing.
4. **`FarmhandNpc` needs no client-inertness override.** `Character.update` already runs
   `updateMovement`/`controller.update` only under `Game1.IsMasterGame` and otherwise calls
   `updateSlaveAnimation`, so an override would have been dead code. Verified against the decompile
   and recorded in the game-data file; S11 still confirms it in-world.
5. **There is no separate read-only card.** `ContractMenu` is the one office page and
   `OfficeViewerRoles` decides which of its actions exist for the player at that screen — a second
   card would be a second thing to keep in step for no gain.
6. **`Handshake` is not its own class.** The peer handshake, the incompatible-peer policy, and
   message dispatch are all one small subscriber (`DaysworkNetwork`), per the no-ceremony rule.
7. **Notices route through a new `OwnerNotifier`**, not through `ShiftOutcomeDispatcher` alone —
   it is the notice counterpart to `Sponsor` and picks HUD / message / log by where the owner is.
   **Scope limit:** `CropHudNotifier`'s managed-crop notices stay host-local. They are deduplicated
   across every one of the day's workers, so they have no single owner to address; routing them
   would mean threading an owner id through that whole pipeline and giving up the shared dedup.
   Recorded in AGENTS.md as a known limitation.
8. **A client re-reads contracts from the world when it opens an office**
   (`OfficeContractPersistence.RehydrateFromWorld`), rather than gaining a refresh protocol.
   `Building.modData` is synced, so this is all a live card needs.
9. **Day start skips a contract whose owner no longer exists** (rather than only handling orphans in
   the UI), with a log line; the host's Claim action is what un-sticks it.

**Acceptance.** The whole multiplayer section of the smoke matrix below, including split-screen.
Built and unit-tested 2026-09-07 (build + 781 tests green); **S10–S31 are unrun** — the in-game
smoke passes for Phases 1–4 are deliberately deferred until every phase is built.

## Phase 5 — Paintable offices

**Problem.** Robin's paint menu refuses the office.

**Design.** Per the game-data file: author `assets/farmhand_office_PaintMask.png` (160×122; red /
lime / blue regions for the walls / roof / trim; glow and smoke frames unmasked) served as
`Mods/Bindicle.Dayswork/Building_PaintMask`, and edit `Data/PaintData` to add an entry for
`Bindicle.Dayswork_Office`. The evening overlay draws the glow from `building.texture.Value` (the
painted texture) instead of reloading the raw sheet so a repainted wall and its lit window agree.
Nothing to persist or sync (`netBuildingPaintColor` is both).

**Two changes to this design, made while building it (2026-09-07).** Both came out of dumping the
real `Data/PaintData` and `Strings/Buildings` rather than assuming their contents — the numbers and
names are now recorded in [`../game-data/painting.md`](../game-data/painting.md):

1. **No `Strings/Buildings` edit, and the regions are named `Building` / `Roof` / `Trim`, not
   `Walls`.** Every vanilla `Data/PaintData` entry already uses those three names, and vanilla
   already ships `Paint_Region_Building|Roof|Trim` translated in all 13 languages. Reusing them
   gets the office localized labels for nothing; the planned `Walls` would have meant shipping a
   string of our own *and* overwriting one of vanilla's keys to place it.
2. **The brightness pairs are `-20 20` / `-15 10` / `-15 10`, not `-100 100`.** The two numbers are
   the region's lightness slider range, and no vanilla building uses the parser's `-100 100`
   fallback — they are tuned per building to stop a paint job crushing that region's shading ramp.
   These match the Log Cabin's, the building the office sprite is modelled on.

**Acceptance.** Robin → Paint Buildings lists the office; three regions repaint; the colour
survives save/load and is visible to clients; the lit-window overlay matches the painted base; an
unpainted office renders byte-identical to today.

Built 2026-09-07 (build + 786 tests green, five of them new). **S32 is unrun** — like Phases 1–4,
its in-game smoke pass is deferred.

---

## Smoke-test matrix

Run with `DevLog.Enabled` where noted; every row lists the phase after which it must pass.

| # | Scenario | Phase | Pass when |
|---|---|---|---|
| S1 | Single-player regression, one office (hire, all task families, managed crops + shopping, machines, ponds, deposits, overflow, 8 pm cap, sleep) | 1 | Behaviour identical to 1.2.1 except the office name |
| S2 | Single-player, 3 offices, overlapping zones, one shared input chest per office; fell nearby trees concurrently and shake/chop fruit trees and stumps | 1 | Three workers, own doors, claims prevent double work, and each tree's exact wood/sap/seed/quality/flavor output lands only in that worker's configured destination; nearby player drops stay in the world; fruit/stump output is neither stolen nor duplicated; three lit offices |
| S3 | Single-player, 2 offices both auto-buying seeds under one wallet | 1 | No overdraft; second worker skips the trip if the ledger says it cannot afford it |
| S4 | Save/load with 3 active contracts (2 recurring, 1 one-time) | 1 | Contracts round-trip from modData; one-time fires exactly once |
| S5 | 1.x save adoption | 1 | Contract bound to the office, owner = host, old key holds the v4 marker, no double charge |
| S6 | Demolish an office mid-shift, including during outbound shopping, store-open waiting, partial purchasing, and return travel | 1 | Worker stops; no later purchase occurs; every paid supply reaches the office input chest when it survives or the owner's shipping fallback when the office is gone/full; shopping reservations release; forfeit notice shown; contract gone; only completed purchases move money |
| S7 | SVE Grandpa's Farm / Frontier Farm, 2 offices, greenhouse + shed scope | 1 | Expansion hops still route; each worker returns to its own door |
| S8 | XP toggle on/off (skills page before/after a full shift) | 2 | On: crops + fieldwork grant XP to the owner once; off: no skill change; machines still collect |
| S9 | Appearance presets on 2 workers, reload | 3 | Distinct palettes persist |
| S10 | Host, no clients connected, 2 offices | 4 | Identical to S2 |
| S11 | Client joins mid-shift (SMAPI + matching protocol, including a different compatible 2.x patch version) | 4 | Handshake completes promptly without settling the live shifts; client sees both workers, names, stamina bars, lit offices; no shift loop on client (log shows none) |
| S12 | Client leaves mid-shift, `RunWhileOwnerOffline` on / off, including while shopping is outbound, waiting, partially purchased, and returning; advance to the next morning in both modes | 4 | On: their worker finishes and remains eligible next day. Off: no later purchase occurs, paid supplies/output are delivered, the reservation releases, and the next morning makes no charge, spawn, or one-time consumption while that owner remains absent |
| S13 | Owner is absent when the host loads/starts a day; test `RunWhileOwnerOffline` on/off with recurring and prepaid one-time contracts, including a paused contract and season rollover | 4 | On: normal charge/work behavior (items to owner's bin, no offline XP). Off: recurring stays active without charge and prepaid one-time stays active overdue; on the first connected eligible morning the one-time contract runs exactly once without another payment |
| S14 | Shared wallets | 4 | Two owners' contracts both charge the same money |
| S15 | Separate wallets; two owners fell nearby trees into separate destinations with the host both present and in another location | 4 | Each owner's own wallet and shipping bin; each worker receives only its own tree emissions in either update order, including resource-chunk quantities and real-item traits |
| S16 | Mid-save wallet switch (Manor House, sleep) | 4 | Next-day charges and shipping follow the new mode without restart |
| S17 | Save/load co-op with 3 owners' contracts | 4 | All contracts survive with their owners and revisions |
| S18 | Client hires from its own office | 4 | Menu opens, draft builds, commit accepted, worker appears next morning |
| S19 | Client edits an existing contract | 4 | Revision increments; host and client show the same contract |
| S20 | Host rejects: insufficient funds (one-time) | 4 | Client sees the reason, draft retained |
| S21 | Host rejects: chest removed between draft and commit | 4 | `ChestMissing` shown, draft retained |
| S22 | Client disconnects between send and commit | 4 | Host commits if received; client sees the contract on rejoin |
| S23 | Non-owner clicks someone else's office (guest / host) | 4 | Read-only card; host sees Pause/Cancel; nobody can edit |
| S24 | Orphaned office (delete the owner's farmhand slot) | 4 | Skipped at day start with notice; host can Claim |
| S25 | Client tries to pick an SVE expansion location / an off-farm input chest | 4 | Picker shows only the host-snapshot entries; "waiting for host…" until it arrives |
| S26 | Peer without Dayswork joins (SMAPI, no mod) while a worker is live; repeat with a real 1.2.1 pre-protocol build and with a compatible late joiner after suspension | 4 | Workers despawn before approval for no-mod/legacy guests, host warns, chat is visible, and spawns stay refused. Under kick policy cleanup still precedes the snapshot and the kick occurs only after approval. A late compatible joiner immediately mirrors the existing suspension reason; the last offender leaving clears both clients' banners |
| S27 | Vanilla (no SMAPI) peer joins while a worker is live, under both suspension and kick policies | 4 | Worker is absent from the world snapshot; default policy suspends until disconnect; kick policy performs the deferred approved-peer kick and clears pending state on disconnect. Record the actual transport outcome, not only the log |
| S28 | Type-compatible 2.x peer with a modified protocol constant; also connect 2.x to a real 1.2.1 host | 4 | Different protocol can deserialize workers but cannot mutate or spawn; handshake mismatch/timeout produces `VersionMismatch` and the configured suspension/kick policy. A 2.x client identifies the 1.x host as incompatible before offering editable menus |
| S29 | **Split-screen** (host + one local guest), including nearby concurrent tree falls for separate owners | 4 | Exactly one shift loop; guest's own office is editable via the loopback request path; ownership checks are per screen; menus do not interfere. Join the guest mid-shift while one worker has buffered output, another carries paid shopping supplies, and a tree is falling; guest load/day-start/exit must preserve both workers, output, purchases, wallet reservations, work claims, upgrades, and tree attribution. Only the host lifecycle/tick may reset the engine or capture delayed drops |
| S30 | Two workers at Pierre's counter at once | 1/4 | Both purchase; no stuck; log shows no ledger overdraft |
| S31 | Guest has the destination chest open when a worker arrives | 4 | Mutex wait then overflow; nothing lost |
| S32 | Paint the office; guest paints host's office | 5 | Colour syncs, overlay matches, survives reload |

---

## Risks and things flagged rather than designed around

- **Host-identity action farmer means the owner's professions never apply** to worker actions (the
  fake farmer has none; `Crop.harvest` uses the *host's* Tiller etc.). This is today's behaviour in
  single-player (player == host). Accepted for 2.0; recorded here so it is not "discovered" later.
- **Vanilla-guest join crash** (correction 8) may be unpreventable; Phase 4 measures and documents.
- **`Farmer.modData` for per-owner data was considered and not chosen**: whether the host may write
  to another player's `Farmer` root is not verified, and the game routes such writes through
  explicit messages elsewhere. Upgrades stay in host save data.
- **modData authority is a discipline, not an enforcement.** Any code path that writes office
  modData off-host is a bug; a debug assert in `OfficeContractStore` guards it.
- **Worker–worker physical blocking** at doors/counters is untested; the stuck detector is the
  backstop.
- **Scope creep guard:** continuous world-state sync to clients, free HSL appearance sliders,
  per-office upgrades, and the hub-and-satellite topology are all out.

---

## Decisions resolved with the user (2026-09-07)

1. **XP toggle** is a per-contract preference `GrantExperience` on the Preferences spoke. No global
   switch.
2. **Farmhand upgrades** are per owner, in host save data, delivered to clients in the menu snapshot.
   Single-player behaviour is unchanged.
3. **Incompatible peer** default is suspend-and-warn; `KickIncompatiblePeers` is an opt-in config.
4. **Demolishing an office with an unexecuted one-time contract forfeits the price.** No refund
   path is built anywhere (D1, D6, Phase 1).
5. **Phase order** as written: worker appearance (Phase 3) ships before multiplayer (Phase 4).
6. **Non-owner interaction:** read-only card for everyone; the host additionally gets Pause / Cancel
   on any contract and Claim on orphaned offices.
7. **Rename now:** "Farmhand Cabin" → "Farmhand Office" across building, chests, labels, and
   notices in Phase 0, with a changelog entry.
8. **No XP for offline owners.** Worker XP earned while the owner is not connected is discarded,
   never banked or persisted (corrections 2/3, D10, Phase 2).
