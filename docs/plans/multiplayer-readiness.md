# Multiplayer Readiness Analysis

**Status:** analysis only. Dayswork is **not multiplayer-ready today**. The current implementation is
intentionally single-player: key player entry points return early in multiplayer, and the shift
engine was built around one local player, one contract store, one worker NPC, and one mutable shift
session.

This document describes the effort to make the mod work safely in Stardew Valley multiplayer while
preserving the core rules: no Harmony, one active contract, items are never lost, and the worker is
removed before save.

## Current behavior

Dayswork currently treats multiplayer as unsupported, not partially supported.

- `Dayswork/Guards/MultiplayerGuard.cs` returns `Context.IsMultiplayer`.
- `Dayswork/Integration/HiringBuildingInteraction.cs` blocks the office bulletin-board interaction
  when that guard is true.
- `Dayswork/Orchestration/RecurringContractScheduler.cs` also skips day-start contract scheduling in
  multiplayer.
- Other event hooks are still registered by `Dayswork/ModEntry.cs` for content assets, save
  lifecycle, rendering, input, and update ticks. That means the current state is best described as
  "feature-disabled in multiplayer"; it should not be advertised as multiplayer-compatible.

Removing those guards is not enough. If clients and host all ran the scheduler or shift loop, the
worker could duplicate world mutations, item collection, payments, deposits, machine loads, or save
state writes.

## Recommended support model

The lowest-risk v1 is **host-authoritative, single-worker multiplayer**:

- One shared office building.
- One shared active/paused contract, matching the current core invariant.
- One shared farmhand NPC and one active `ShiftSession`.
- The host/main player owns persistence, scheduling, shift simulation, world mutations, payment, and
  final item routing.
- Farmhands install the same Dayswork version for assets, menus/status display, building content, and
  worker visuals, but clients do not run contract scheduling or worker execution.

This model fits the current architecture. The shift engine already assumes one mutable session and a
single active contract. Multi-worker or per-player contracts would be a separate design, not an
incremental multiplayer enablement.

### Multiplayer setup

Recommended player-facing setup for v1:

1. The host installs Dayswork and builds the office.
2. Joining farmhands install the same Dayswork version. The host should warn when a farmhand connects
   without the mod or with an incompatible version.
3. The office remains a shared farm feature. Contract creation/editing should be host-only for the
   first multiplayer release, or client actions should be sent as host-validated requests.
4. The worker runs from the host's simulation. Clients see synced world state, synced worker/status
   visuals, and read-only contract/shift status.
5. Payment and tool progression use an explicit owner. For v1, the recommended default is the
   host/main farmer. Separate-wallet support should either be documented as host-paid/host-credited
   or implemented with a clear contract sponsor.

## Work needed

### Authority and lifecycle

Add a multiplayer authority layer to distinguish single-player, host, remote client, and split-screen
contexts. The host should be the only peer that reads/writes Dayswork save data, charges money,
starts recurring shifts, advances the shift orchestrator, mutates farm objects, deposits items, buys
shop goods, or despawns the worker before save.

Clients should still load passive content and visuals. Client-side event handlers need clear gating so
they cannot run a second shift loop or write save data.

### Persistence and sync

Current persistence uses SMAPI save data through `ContractPersistenceAdapter`, which must remain
host-only. Clients need a read-only snapshot sent by the host if they can open status UI or see
contract details.

Needed sync messages include:

- Peer/version handshake.
- Current contract summary and schedule state.
- Shift state such as active/idle, remaining energy, current task/status, and stop reason.
- Office visual state such as work-completed overlay.
- Host validation results for any client request.

### UI and permissions

The safest v1 UI is host-editable and client-read-only. If clients can request contract edits, the
host must validate every request against live host state: one active contract, chest references,
machine selections, work zones, affordability, schedule rules, and save compatibility.

Client menus should treat local state as informational. They should not directly mutate the contract
store, spend money, select output chests for persisted state, or start/cancel shifts.

### Ownership of money, tools, shipping, and player-bound APIs

Many current actions use `Game1.player`. In multiplayer, that is whichever player is local to the
running peer, so it is not a stable ownership model.

Before enabling multiplayer, the mod needs an explicit worker owner/sponsor for:

- Upfront recurring and one-time payments.
- Tool-level inheritance.
- Shipping-bin routing, especially when separate wallets are enabled.
- Fake farmer/action contexts for crop, animal, machine, and shop APIs.
- HUD/audio/message suppression that currently assumes the local player.

Recommended v1 default: the host/main farmer is the sponsor. A later cooperative release can let the
host choose a sponsor or bill the requesting player.

### Shift execution and world safety

The shift orchestrator should run only on the host. This includes scanning work, pathing the worker,
running guarded crop/tool/animal/machine actions, depositing output, buying managed-crop supplies,
handling stuck recovery, stopping at sleep, and clearing the NPC before save.

Output safety rules still apply. Multiplayer adds extra concurrency pressure:

- Chests can be open or locked by another player.
- Machines can be collected, moved, or loaded by players while the worker is traveling.
- Separate wallets can affect shipping credit.
- Remote clients can join, disconnect, or sleep mid-shift.

Existing fallback routing should remain: selected chest -> office output chest -> shipping bin, with
no dropped/lost items.

### Visuals and NPC sync

The worker NPC is added to `GameLocation.characters`, which is net-backed in the game, so a
host-spawned worker is the right direction. Dayswork-specific visual state still needs review:

- The stamina/energy bar on `FarmhandNpc` is stored in local private fields, not net fields.
- The office completion overlay uses `HiringBuilding.WorkCompletedToday`, which is static local mod
  state.
- Clients may need explicit status messages for worker task text, energy, and completion state.

Farmhands should install the mod so custom building assets, worker assets, and i18n text are present
locally.

## Effort estimate

| Scope | Estimate | Notes |
|---|---:|---|
| Documentation only | Low, 0.5 day | This document plus recorded game/SMAPI findings. |
| Minimal host-only multiplayer | Medium, 3-5 engineering days plus smoke testing | Host can use Dayswork safely in a multiplayer save; clients install the mod but mostly view synced state. |
| Cooperative client requests/UI | Large, 1.5-3 weeks | Adds host-validated request/response protocol, permissions, conflict handling, resync, and richer client UX. |
| Multiple workers or per-player contracts | Very large, 4+ weeks/high risk | Conflicts with the current single-contract and single-session architecture; would need new scheduling, routing, ownership, collision, and persistence design. |

Recommended milestone order:

1. Host-only authority gates and single-player regression.
2. Host-owned money/tool/shipping/sponsor model.
3. Mod-message handshake and read-only client status sync.
4. Remote-client and split-screen smoke pass.
5. Optional client request flow after the host-only model is stable.

## Test and smoke matrix

Before claiming multiplayer support, verify:

- Single-player regression: existing office, hiring, shifts, deposits, managed crops, machines, and
  saving still work.
- Host multiplayer with no connected clients.
- Remote farmhand joins with matching mod version.
- Remote farmhand missing or mismatched mod version gets a clear warning/limitation.
- Split-screen, because SMAPI treats it as multiplayer.
- Shared wallet and separate wallet saves.
- Client joins mid-shift, disconnects mid-shift, and reconnects.
- Sleep/save during an active shift; worker is removed before save.
- Chest busy/locked by another player; output falls back safely.
- Machine collect/reload while another player interacts with machines.
- SVE expansion farms and farm-expansion travel still route through host authority.

## Verified game and SMAPI findings

Confirmed against the local Stardew Valley install and SMAPI XML docs under
`X:\Steam\steamapps\common\Stardew Valley`, plus decompiled game code where noted:

- `Context.IsMultiplayer` is true for multiplayer and split-screen contexts, so the current guard
  intentionally blocks both.
- SMAPI exposes multiplayer peer events and `Helper.Multiplayer.SendMessage<T>` for mod-to-mod
  synchronization between connected players.
- SMAPI save data helpers are for the current save and require the main player for save-slot access,
  so Dayswork contract persistence should be host-only.
- `GameLocation.characters` is a net collection, and `GameLocation.addCharacter(NPC)` adds the NPC to
  that collection. A host-spawned farmhand NPC is the correct base model for remote visibility.
- `Farm.getShippingBin(Farmer who)` chooses the player's personal shipping bin when separate wallets
  are enabled, otherwise it uses the shared shipping bin. Dayswork must pass an intentional farmer
  owner for shipping in multiplayer.
- `Game1.IsMasterGame`, `Game1.MasterPlayer`, and online-farmer APIs provide game-side authority and
  player lookup signals that are relevant for host/client decisions.
- `CreateLocationData.AlwaysActive` keeps locations synchronized to farmhands in multiplayer; any
  future custom or expansion location assumptions should account for that flag.
- `ShopItemData.AvailableStockLimit` combines with `LimitedStockMode` (`Global`, `Player`, or
  `None`) for multiplayer shop stock behavior. Managed-crop auto-buying should be explicit about
  which farmer/sponsor performs purchases.

## Recommendation

Do not advertise the current mod as multiplayer-ready. The practical path is to keep the one-worker,
one-contract design and make it host-authoritative first. That preserves the existing architecture,
keeps item safety tractable, and leaves cooperative UI or per-player labor systems as later features
instead of forcing them into the initial multiplayer pass.
