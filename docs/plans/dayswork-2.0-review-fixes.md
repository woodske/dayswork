# Dayswork 2.0 — implementation review and corrective plan

Review date: 2026-09-08. Reviewed branch `dayswork-2.0`, commits `b4e2394` through
`c742009`, against the pre-implementation base `5b3c231` and
[the 2.0 plan](dayswork-2.0.md). Tracking belongs in [index.md](index.md).

This document records concrete findings and the work to fix them. The review changes documentation
only; the fixes below are not implemented by this review. The existing implementation builds with
deployment disabled, and `dotnet test Dayswork.Tests/Dayswork.Tests.csproj -p:EnableModDeploy=false
--no-restore --verbosity minimal` passed **786 tests, with one intentional skip**. No in-game
smoke tests were performed. The original S1–S32 matrix remains a release gate.

## Findings

P1 means fix before release because it can lose items/purchases or defeat multiplayer compatibility.
P2 means a functional defect that also needs correction before declaring the affected feature ready.
Line numbers refer to the reviewed commit, `c742009`.

| ID | Priority | Finding | Primary source |
|---|---|---|---|
| R1 | P1 | Joining a second local screen resets the host's workers and erases purchased upgrades. | [FarmhandUpgradePersistenceAdapter.cs](../../Dayswork/Integration/FarmhandUpgradePersistenceAdapter.cs):35–40; [SessionResetHandler.cs](../../Dayswork/Orchestration/SessionResetHandler.cs):28; [ShiftFleet.cs](../../Dayswork/Orchestration/ShiftFleet.cs):124–128 |
| R2 | P1 | An early stop during shopping can discard already-paid supplies and strand budget reservations. | [ShiftOrchestrator.cs](../../Dayswork/Orchestration/ShiftOrchestrator.cs):219–242; [ShiftOrchestrator.Deposit.cs](../../Dayswork/Orchestration/ShiftOrchestrator.Deposit.cs):81–129 |
| R3 | P1 | Kick mode skips the cleanup needed before the joining peer receives the world. | [DaysworkNetwork.cs](../../Dayswork/Net/DaysworkNetwork.cs):140–147 |
| R4 | P1 | A peer with the Dayswork mod ID but no handshake is treated as compatible indefinitely. | [DaysworkNetwork.cs](../../Dayswork/Net/DaysworkNetwork.cs):83–88, 219–224, 344–357 |
| R5 | P2 | Disabling offline work does not prevent next-day charges or worker spawning while the owner is absent. | [RecurringContractScheduler.cs](../../Dayswork/Orchestration/RecurringContractScheduler.cs):107–154 |
| R6 | P2 | Nearby workers can collect each other's delayed tree drops into different owners' output. | [ShiftOrchestrator.Debris.cs](../../Dayswork/Orchestration/ShiftOrchestrator.Debris.cs):229–235 |
| R7 | P2 | Remote contract actions refresh a stale local store, leaving the menu inconsistent with accepted changes. | [ContractMenu.cs](../../Dayswork/UI/ContractMenu.cs):158–160, 362–375; [ContractRequestClient.cs](../../Dayswork/Net/ContractRequestClient.cs):133–143 |
| R8 | P2 | A remote player opening Upgrades from Manage never requests their purchased-upgrade state. | [HiringFlowCoordinator.cs](../../Dayswork/UI/HiringFlowCoordinator.cs):86–89, 147–174; [MenuSnapshotCache.cs](../../Dayswork/Net/MenuSnapshotCache.cs):49–64 |
| R9 | P2 | The client can display one price and energy allowance while the host commits different terms. | [HiringFlowCoordinator.cs](../../Dayswork/UI/HiringFlowCoordinator.cs):1074–1075; [ContractRequestHandler.cs](../../Dayswork/Net/ContractRequestHandler.cs):85–133 |
| R10 | P2 | A compatible late joiner receives no snapshot of an already-suspended farm. | [DaysworkNetwork.cs](../../Dayswork/Net/DaysworkNetwork.cs):153–166, 258–267 |
| R11 | P2 | Start, exhaustion, and unreachable-building notices still go to the host instead of the worker's owner. | [ShiftOrchestrator.cs](../../Dayswork/Orchestration/ShiftOrchestrator.cs):388–394; [ShiftOrchestrator.Deposit.cs](../../Dayswork/Orchestration/ShiftOrchestrator.Deposit.cs):198–202; [ShiftOrchestrator.Travel.cs](../../Dayswork/Orchestration/ShiftOrchestrator.Travel.cs):381–383 |

## R1 — Separate host lifecycle from local-screen lifecycle

**Failure.** Load a farm with purchased upgrades, let workers collect output, then join with a local
split-screen guest. SMAPI raises `SaveLoaded` and `DayStarted` for that guest's screen. The shared
`SessionResetHandler` resets the fleet; each orchestrator despawns and discards its session without
settling the buffer. The upgrade adapter's non-host branch clears the shared owner-upgrade store,
which the host then writes empty at the next save. The guest's `DayStarted` also replaces `FleetDay`.

This is verified against the installed SMAPI decompile: see
[split-screen-lifecycle.md](../game-data/split-screen-lifecycle.md). Objects built once in
`ModEntry.Entry` are shared across local screens; per-screen events do not construct new services.

**Implementation.**

- Gate shared fleet resets, day-state creation, and upgrade-store load/clear operations to the
  actual host/session lifecycle. A guest joining or leaving must not mutate these objects.
- Audit `ReturnedToTitle` separately using event-time context; a guard that works inside the world
  must not prevent cleanup when the host actually exits. Preserve normal host load/title resets.
- Scope `ContractRequestClient` pending-request cleanup to the departing/loading screen. Its current
  `ResetAllScreens` must not cancel another screen's pending callbacks on a guest lifecycle event.
- Keep shared host data shared. Do not solve this by giving every screen its own shift engine or
  upgrade store. Audit other shared mutable state reset from the same event registrations.

**Acceptance.**

- [ ] Regression coverage drives host load, guest load/day-start, guest exit, and host exit in order;
  guest events preserve workers, buffers, shopping reservations, and work claims.
- [ ] Purchased upgrades survive local guest join, the next host save, and reload.
- [ ] S29 is expanded to include a guest joining mid-shift while one worker has output and another
  carries purchases; only the host runs and resets the engine.

## R2 — Settle shopping before an external early stop

**Failure.** Demolish an office, or disconnect its owner with offline work disabled, while its
worker walks back from a shop with purchases. The new callers reach `EndShiftEarly`, which calls
`QueueWrapUpNow` and terminal deposit. Bought items remain only in the shopping coordinator's
`_carriedItems`; terminal deposit settles machine inputs but not shopping purchases. Normal exit
then discards the session. `CompleteReturn`, which would settle purchases and release the wallet
reservation, is never reached. Stopping during store waiting or purchasing can instead leave those
shopping tick branches running over the new deposit travel.

The direct disconnect call is in `DaysworkNetwork.EndShiftsForDepartedOwner`; demolition calls from
`OfficeDemolitionHandler`. Menu Pause/Cancel do not currently invoke this early-stop path.
This defect follows from mod-owned lifecycle code; it does not depend on a guessed vanilla API.

**Implementation.** Add an interruption operation to the existing shopping coordinator. It must
prevent further purchases, settle carried items exactly once, release its reservation, clear its
runtime phase, and transfer control of travel back to the orchestrator. Call it before terminal
deposit can discard or replace shopping travel. Reuse it where appropriate for sleep/suspension;
do not invoke a normal shopping completion callback that resumes work during shutdown. Preserve the
office-input → overflow → owner's shipping fallback when the office has gone, and preserve walking
through the existing travel primitive.

**Acceptance.**

- [ ] Interrupt outbound travel, waiting for opening, partial purchasing, and return travel.
- [ ] Purchased quantity equals delivered quantity; the wallet is charged only for completed
  purchases; no purchases occur after interruption; reservations are released.
- [ ] Repeated interruption/sleep callbacks do not duplicate output or resume work.
- [ ] Expand S6/S12 with shopping in progress, including a demolished or full input chest.

## R3 — Clean up before approval even when kick mode is enabled

**Failure.** `HandleIncompatiblePeer` calls `Game1.server.kick` during `PeerContextReceived` and
returns without suspension/settlement. All installed transports look up an approved player-to-
connection mapping to kick. That mapping does not exist yet, so the first kick does nothing. The
`PeerConnected` retry occurs after the world snapshot was sent, potentially including live custom
workers that a vanilla/modless guest cannot deserialize.

The installed SMAPI actually raises `PeerContextReceived` before approval for vanilla guests too.
Both facts were verified against local SMAPI, GameServer, Steam, LAN, and Galaxy decompiles and are
recorded in [multiplayer-handshake.md](../game-data/multiplayer-handshake.md).

**Implementation.** Always perform immediate host cleanup when an unsafe peer is detected. Under
kick policy, retain a pending kick and perform it once the connection can be addressed, checking
the approved-peer event rather than assuming the first call succeeded. Clear pending state on
disconnect/rejection/session reset. Prevent the already-incompatible guard from suppressing the
deferred kick. Announce the actual outcome, and do not restart settled shifts mid-day.

**Acceptance.**

- [ ] An event-sequence regression asserts no live worker at the world-snapshot boundary for
  default and kick policies, then verifies the deferred kick and cleanup of its pending state.
- [ ] S26/S27 cover a live worker plus vanilla and SMAPI-without-Dayswork guests on the actual
  transport. No claim of safe connection behavior rests on a log saying an early kick was sent.

## R4 — Require a completed protocol handshake

**Failure.** The host sends `Hello` to anything with the Dayswork mod ID, but records neither a
pending handshake nor a deadline. Only a received wrong-version acknowledgment triggers rejection.
The 1.x implementation has the same mod ID and no protocol handlers, so it never replies and the
host continues running. Conversely, a 2.0 client joining a 1.x host offers menus whose requests
cannot be answered. A missing reply is never evidence of equal protocol versions.

**Implementation.** Track unverified, compatible, and incompatible peers in the existing network
subscriber. Use a bounded handshake deadline on both sides and retry/initial-state exchange at
the connected/save-loaded boundaries where needed. Gate request dispatch and spawning on confirmed
compatibility. Handle known pre-protocol releases before approval using verified version metadata;
do not replace protocol equality with exact mod-version equality for supported patch releases.
The reviewed branch and its 1.x base both still advertise `1.2.1` in `manifest.json`: assign distinct
2.0 release/prerelease metadata before using a supported-version floor to identify legacy peers.
Do not infer protocol support from that currently ambiguous version during development.
Unverified peers must not open an unsafe world-sharing window while a timeout runs. Keep the
early-cleanup and deferred-kick rules from R3, including reconnect and failed-join cleanup.
Distinguish known unsafe/legacy builds from supported builds awaiting a reply: D8 already permits
supported protocol-mismatched builds to resolve the worker type. Pending confirmation for a build
verified to understand that type can hold execution without discarding live shifts; known unsafe
builds need cleanup before the snapshot. A failed or timed-out handshake invokes the configured
suspension/kick policy. Do not mistake type-level safety for permission to run a different protocol.

**Acceptance.**

- [ ] Regression scenarios cover same mod ID/no response, 1.x host, 1.x guest, wrong protocol,
  matching protocol/different compatible patch version, disconnect before acknowledgment, and
  reconnect. Unsupported peers never permit shifts or mutations merely by having the mod ID.
- [ ] Exercise S11/S26/S28 with a real pre-protocol build as well as a modified version constant.
  A promptly compatible mid-shift join must preserve S11's observable workers; do not make every
  successful handshake end the day's shifts as an incidental effect of the pending state.

## R5 — Honor the offline preference at the scheduling boundary

**Failure.** `StartOne` checks whether the owner exists, not whether the owner is online. An offline
farmer resolves through `Game1.GetPlayer`'s saved `farmhandData` lookup, as confirmed again against
the local game decompile and documented in [multiplayer-and-ownership.md](../game-data/multiplayer-and-ownership.md).
Thus an owner who disabled offline work is charged and gets a new worker the morning after leaving,
or when the host loads without them. The disconnect-only check cannot prevent this.

**Implementation.** Before charging, consuming a one-time contract, or spawning, require either
`RunWhileOwnerOffline` or `Sponsor.IsConnected(ownerId)`. Keep owner existence and connectivity
distinct. Leave recurring contracts active and charge only on eligible mornings.

Complete the previously unspecified prepaid case: defer an offline-disabled one-time contract to
the first eligible morning, without another charge. Update the one-time due-date rule so an active,
unexecuted contract does not become permanently unreachable after its hire-date-plus-one passes.
Mark it executed before its actual start as today. Cover overdue scheduling, pauses, festivals,
and suspension in the tests; do not introduce catch-up billing for recurring contracts or change
demolition's no-refund rule.

**Acceptance.**

- [ ] Offline/false means no new charge, spawn, or consumption; offline/true keeps current behavior;
  online/false runs normally. Test load-with-owner-absent as well as disconnect-then-next-day.
- [ ] A skipped prepaid contract runs once on its first eligible morning, with no second payment;
  save/reload and date/season rollover preserve that result.
- [ ] Expand S12/S13 with both preference settings and the prepaid case.

## R6 — Prevent cross-worker capture of delayed tree drops

**Failure.** Each session holds an old debris snapshot for 240 ticks and sweeps new items within
six tiles of its work. The game emits trunk drops later from `Tree.tickUpdate`, four tiles left or
right of the trunk; multiple falling trees can emit before the next fleet callback. If workers
fell nearby trees, the first sweep can take both sets and credit its own owner/destination. Per-tile
work claims and sequential synchronous action guards do not establish ownership of these later
emissions. See the verified [tree-debris analysis](../game-data/multi-worker-delayed-debris.md).

**Decision approved 2026-09-08.** Use a narrowly scoped Harmony hook around `Tree.tickUpdate` to
attribute drops to the actual worker-felled tree. This replaces the proposed per-location collection
lease: workers should remain free to fell separate trees concurrently. The user approved relaxing
the blanket no-Harmony rule to SMAPI events first, with narrow exceptions where they remove
substantial complexity. This decision does not authorize broad XP, loot-creation, or networking
patches; the other review fixes keep their event/request implementations.

The verified boundary is a single tree update: vanilla creates that tree's delayed trunk drops
synchronously during the call, before `GameLocation` acts on its return value. A paired prefix and
postfix can therefore compare debris immediately around that call rather than infer ownership from
several seconds of nearby activity. Use finalizer cleanup for exceptions. No transpiler is needed.

**Implementation.**

1. Enable Harmony through the mod's build configuration and wire explicit patch registration from
   `ModEntry`. Keep the patch and all game-object tracking in `Dayswork/`; Core gains no Harmony or
   Stardew references. Target the verified `Tree.tickUpdate` overload only. Register once per
   process and gate capture/mutation to the authoritative host, including split-screen.
2. Associate each actual worker-felled `Tree` instance with its owning shift, owner, location, and
   output provenance when a worker action starts its fall. Do not use a tile alone or the shared
   host `lastPlayerToHit` identity as the association. Unregistered/player-felled trees take the
   unchanged vanilla path.
3. In the prefix for a registered tree, capture that location's existing debris references in
   per-call state. In the postfix, capture only newly added collectible debris from this call and
   transfer it to that shift's buffer before the caller can remove the tree. Keep vanilla animation
   and return behavior. Remove debris from the world only after the receiving buffer owns it.
4. Preserve full item fidelity. For `Debris.item`, use the existing real-item capture/clone registry
   and carry quality and other traits, not only qualified ID and quantity. Preserve resource-chunk
   quantity handling. The current `CollectNewDebris`/`TryGetDebrisItem` implementation cannot simply
   be reused unchanged because it reduces real items to ID/quantity.
5. Make postfix/finalizer settlement and cleanup idempotent. On a thrown update, preserve any output
   already emitted, release per-call state safely, and preserve the original exception rather than
   hiding it. Retire tree ownership after its fall is settled; handle removal/replacement explicitly.
   If patch registration fails, disable the dependent tree work with an operational diagnostic;
   do not silently resume the inaccurate broad sweeps.
6. Keep offscreen fall advancement: Harmony observes calls but does not make an unticked tree
   advance. On sleep, demolition, owner disconnect, or suspension, complete/settle registered falls
   before discarding their sessions, or retain an owner-specific safe output sink until settlement.
   Clear associations on real session teardown without disturbing other local screens (R1).
7. Remove the historical-radius sweeps used for tree attribution. Audit fruit and stump collection
   against their actual emission timing too, so a leftover broad sweep cannot steal another tree's
   newly attributed output. Do not treat comments about fruit "settling" as proof that item creation
   is delayed. Use the existing guarded synchronous collection where that is the verified behavior.

Other mods may patch the same method, suppress its original, or create items asynchronously. The
ownership argument above covers vanilla's synchronous emission block; verify patch ordering and
the supported mod combinations rather than assuming all extensions share that boundary. Prefer
this one-method hook over global item-creation interception.

**Acceptance.**

- [ ] Two concurrent worker-felled trees with distinct owners/destinations capture only their own
  emissions, regardless of update order; nearby player-created drops remain outside worker output.
- [ ] Captured real items retain quantity, quality, and distinct traits; resource chunks retain
  their full quantity. Postfix/finalizer retries and thrown updates neither lose nor duplicate items.
- [ ] Offscreen and onscreen falls both settle correctly. Sleep, demolition, owner disconnect,
  suspension, tree replacement, and session teardown leave no orphaned registrations or output.
- [ ] A failed hook cannot start dependent tree work. Ordinary player tree actions remain unchanged;
  guest screens never capture or route drops.
- [ ] Expand S2/S15/S29 with nearby concurrent tree work, separate destinations, and host
  present/absent; compare each worker's output with its own actions. Smoke-test SVE and relevant
  tree/drop-mod combinations, including interactions with fruit and stump collection.

## R7 — Apply authoritative contract state before refreshing remote menus

**Failure.** A remote owner presses Pause. The host accepts and changes its store/modData, but the
client callback calls `ContractMenu.Refresh`, which reads the old client `OfficeContractStore`.
Only opening the building rehydrates that store. The page stays Active with a Pause button even
after acceptance; the next click submits Pause again. Cancel and edit-after-action have the same
staleness problem. Even re-reading modData immediately in the callback can race the world delta.

**Implementation.** Return the authoritative office contract/revision with the action/commit result
and apply it to the client's read cache before callbacks redraw. Reuse the existing serializer and
silent hydration; only the host writes building modData. Handle absent contracts and rejections
explicitly. Correlate replies to the office, request, and screen, and reject older state rather
than overwriting a newer acknowledged revision. Preserve or restore the planned live-shift indicator
so remote Cancel availability does not consult the client's permanently empty fleet.

Capture the edit's base contract ID/revision in its draft. On a stale rejection, retain the draft
and offer an explicit refresh/review route; do not merely send the same stale request forever or
silently replace the draft's base with a later contract. Account for the owner's energy-upgrade
purchase changing open contract revisions while an edit is in progress.

**Acceptance.**

- [ ] Delay the world modData delta until after the acknowledgment: Pause changes to Resume,
  Resume changes to Pause, and Cancel removes the active contract immediately on the client.
- [ ] Edit after an action and purchase-energy-while-editing both reach a usable, accurate flow.
- [ ] Stale/out-of-order responses cannot overwrite newer state or discard authored changes.
- [ ] Expand S19/S21/S23/S31 to check the still-open menu, not only close-and-reopen behavior.

## R8 — Initialize and refresh remote upgrade state on every entry path

**Failure.** On a fresh remote session, opening an existing contract goes directly to Manage,
which does not request a menu snapshot. Upgrades reads `MenuSnapshotCache.Upgrades`, whose null
default is the empty state. Previously owned upgrades look unpurchased; Speed2 remains locked.
Even a successful Speed purchase cannot correct it: `ApplyUpgradeState` is a no-op when the snapshot
is null. Opening Edit first happens to work around this.

**Implementation.** Request owner upgrade state from Manage as well as Hire/Edit, and show a loading
state instead of treating unknown ownership as an empty purchase history. Make the open upgrades
page rebuild when its correlated response arrives. A purchase response must update upgrade state
even if the location/chest snapshot has not arrived. Keep it per screen and avoid late snapshots
overwriting a newer purchase acknowledgment. Reuse the existing snapshot/request mechanism.

**Acceptance.**

- [ ] Fresh connection → existing office → Manage → Upgrades shows previously owned upgrades
  and correctly unlocks Speed2, without first opening Edit.
- [ ] Opening the page before the reply and purchasing before a delayed snapshot are covered;
  no lost ownership display, duplicate charge, or stale relock.

## R9 — Show the host's terms before accepting a purchase

**Failure.** Client previews use the client's `_configManager.CurrentSnapshot`; commits use the
host's. Menu snapshots contain upgrades and locations, but no pricing/energy configuration. For
example, a guest with the default FullDay price sees 500g while a host configured to 1000g charges
1000g if affordable. The submitted terms are silently replaced. Energy/cost settings can disagree
in the same way. Host validation is necessary, but does not make the displayed bargain accurate.

**Implementation.** Include the bounded configuration needed for previews in the host menu snapshot,
or supply a host-generated quote before confirmation. Prefer extending the existing finite snapshot;
do not add continuous world synchronization. Have commits identify the terms the player reviewed
and compare them against freshly computed host terms. If they changed, return the new terms and
require another confirmation before charging. Preserve host authority and per-owner upgrades.

**Acceptance.**

- [ ] Different host/client tier prices, energy settings, and upgrade state produce a displayed
  quote equal to committed terms and the actual wallet debit.
- [ ] Changing host terms after the quote rejects/requotes without spending money; accepting the
  replacement quote charges exactly once. Test insufficient funds and duplicate delivery too.

## R10 — Send current suspension state to joining clients

**Failure.** A compatible guest joins after an incompatible guest already suspended Dayswork.
`DaysworkPaused` was broadcast only at the original transition. `Hello` carries versions only;
`HelloAck.Suspended` is populated on the client and ignored by the host. The late joiner has no
banner and can author changes that are rejected as paused.

**Implementation.** Include the host's current suspension flag, reason, and offending-player name
in the handshake response or a targeted initial snapshot. Apply it before presenting an editable
flow. Continue broadcasting subsequent transitions. Validate that host-state announcements come
from the host, and keep local broadcast delivery from corrupting the host's own state.

**Acceptance.**

- [ ] Incompatible guest → suspension → compatible late joiner: both see the same explanation.
- [ ] The last offender leaving clears both clients' banners; disconnect/rejoin and save switches
  do not retain another session's state.

## R11 — Route the remaining owner-specific shift notices

**Failure.** Shift start, exhausted-worker, and unreachable-building messages call
`Game1.addHUDMessage` directly inside the host engine. A remote owner misses them while the host
gets another owner's notifications. These three messages have an unambiguous session owner and are
outside the accepted shared `CropHudNotifier` limitation.

**Implementation.** Route the three messages through `OwnerNotifier` with the session/contract
owner, preserving translation keys/tokens and the operational unreachable-route log. Keep the
explicitly deferred managed-crop shared-dedup notices out of scope.

**Acceptance.**

- [ ] Host-owned work uses the host HUD; remote-owned work reaches that owner once; offline-owned
  work logs without showing another player's HUD notice. Include a local split-screen owner.

## Implementation order and validation

1. R1 and R2: protect shared state and settlement before changing connection behavior.
2. R3, R4, R10: implement one coherent connection/handshake state flow and initial suspension view.
3. R5 and R6: correct scheduling eligibility and delayed-output routing.
4. R7, R8, R9, R11: make menus, quotes, and notices reflect the authoritative owner state.
5. Run the full automated suite and the expanded smoke matrix. Retain S1–S32; the regressions above
   supplement it rather than replacing the in-game checks.

Add meaningful regression tests for every finding, especially persistence, money, and item routing.
Use a `Dayswork2Review` prefix on regression method names so the index can discover and run this
set without depending on new test-class organization. Prefer existing fixtures and small seams;
do not build a general game simulator or add single-use interfaces just to test these changes.
Changes to message DTOs require a protocol-version bump and serialization tests. Any persistence
format change requires migration tests; most of this plan needs no format change.

Review verification commands (deployment disabled to avoid updating the installed mod during review):

```powershell
rg -n Dayswork2Review Dayswork.Tests
dotnet test Dayswork.Tests/Dayswork.Tests.csproj -p:EnableModDeploy=false --filter FullyQualifiedName~Dayswork2Review
dotnet test Dayswork.Tests/Dayswork.Tests.csproj -p:EnableModDeploy=false
git diff --check
```

The filtered run must actually discover the regression set; a zero-test pass proves nothing.
Record smoke outcomes in the original matrix/tracking surface and do not advertise multiplayer
readiness from unit-test success alone. These fixes concern unreleased 2.0 behavior, so do not add
changelog bug-fix entries as though players had received the broken implementation; adjust existing
Unreleased feature wording only where the intended public behavior changes.

## Decisions retained and limited amendments

**No extreme redesign is recommended.** Retain N offices/one worker each, contracts in building
modData, the host-only sequential fleet, the Sponsor seam, the host-identity action farmer, native
online XP delivery, per-owner upgrades, vanilla painting, and the event-driven engine. The user
approved a narrow Harmony exception for R6 on 2026-09-08. The review found
no confirmed paint-mask or palette defect requiring a design change; S9/S32 remain necessary.

Four bounded amendments should be explicit when updating the original plan:

- **D8's kick assumption changes:** kick mode also needs immediate cleanup, then a deferred kick.
  Installed SMAPI gives vanilla guests the early context event; the old uncertainty about that
  ordering is resolved for this installed version, while transport smoke tests remain required.
- **D3 needs authoritative initial terms and mutation results:** finite snapshots/acknowledgments
  are enough. This adds protocol data, not the continuous world-sync system the plan rejected.
- **D10 needs a prepaid offline-skip rule:** defer to the next eligible morning without another
  charge. This completes a scheduling case left unspecified by the offline opt-out.
- **The integration rule becomes SMAPI events first, with narrow Harmony exceptions.** R6 uses
  before/after hooks around `Tree.tickUpdate` with exception-safe cleanup to capture one tree's
  emissions. This replaces the proposed per-location lease and keeps tree work concurrent. XP
  interception and connection-admission patches are outside this plan.

Permanent first-claim work ownership is unchanged by the R6 attribution hook. The accepted
offline-XP-at-flush behavior and the shared managed-crop notice limitation are not reported as new
bugs.
