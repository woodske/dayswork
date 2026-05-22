# U-14 — Output Pipeline: Code Generation Plan

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stories**: S-04 (completes — orphan chest → mail), S-10 (completes — multi-trip deposit + chest-full/missing fallbacks + refund), S-11 (completes — overflow mail), S-19 (PBT obligations)
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> This plan is the single source of truth for U-14 Code Generation. Generation (Part 2) executes these steps in order **after approval**.

---

## Unit Context

**Components owned (new files)**: C-11 `DepositPlanner` (Core); M-16 `MailDispatcher` (Mod); the deposit/overflow domain types (Core); the runtime MFM adapter (Mod).
**Components extended**: C-10 `ItemBuffer` (+`SourceTask`), C-08 `ShiftStateMachine`/`ShiftIntent` (+`IntentDepositAtChest`), `ShiftContext` (+`TaskDestinations`, +`Overflow`), M-12 `ShiftOrchestrator` (multi-trip deposit + mail flush), M-01 `ModEntry` (construct planner+dispatcher, acquire MFM API), `manifest.json` (MFM dep), `i18n/default.json` (mail keys).
**Reused unchanged**: M-20 `ChestResolver` (`ResolveChest` = chest-liveness oracle), the entire U-13B worker loop (movement, render, stuck, invuln, tool visuals), `RefundCalculator`, `ConfigSnapshot`.
**Dependencies satisfied**: U-04 (TileCoord, TaskKind, DestinationKey, ChestRef), U-06 (Contract.TaskDestinations), U-10 (ItemBuffer, ShiftStateMachine, ShiftOrchestrator), U-11 (ChestResolver), U-13 (ToolMissingWarnings). No forward deps.

**Decisions baked in**: FD-Q1=A (tag buffer items with TaskKind), FD-Q2=A (unassigned→mail), FD-Q3=A (nearest-neighbor Manhattan), FD-Q4=A (queue deliver-tomorrow at shift end, no custom save data), FD-Q5=A (sleep→mail all, no bin dump), FD-Q6=A (one letter, body lists each reason), FD-Q7=A (separate vanilla tool-missing letter). Patterns L–P from nfr-design-patterns.md.

> **Onboarding note (NFR-ONBOARD-01):** new SMAPI surface this unit — `Helper.ModRegistry.GetApi<T>(uniqueId)` to talk to another mod (MFM) at runtime, and `Game1.addMailForTomorrow(mailId)` for vanilla letters. Explained at the relevant steps.

---

## Code Location
- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Core**: `Dayswork.Core\` · **Mod**: `Dayswork\` · **Tests**: `Dayswork.Tests\` (references Core only)
- **Docs**: `aidlc-docs\construction\u-14-output-pipeline\code\`

---

## Steps

### A. Core — domain types, planner, state seams + tests

**Step 1 — Extend `ItemBuffer` to carry the producing task (Pattern L)**
[x] Modify `Dayswork.Core/Inventory/IItemBuffer.cs` + `ItemBuffer.cs`. Add `record BufferedItem(string QualifiedItemId, int Quantity, TaskKind SourceTask)` (in `Dayswork.Core/Inventory/`). Change `Add(string itemId, int quantity, TaskKind sourceTask)`; `Snapshot()` / `TakeAll()` return `IReadOnlyList<BufferedItem>`. Keep validation (non-empty id, qty>0). Pure Core. *S-10; BR-OUT-01; TS-U14-06.*

**Step 2 — Create deposit/overflow domain types**
[x] Create `Dayswork.Core/Inventory/DepositPlan.cs` containing: `record ItemStack(string QualifiedItemId, int Quantity)`; `record DepositTrip(DestinationKey Destination, TileCoord Tile, IReadOnlyList<ItemStack> Items)`; `record DepositPlan(IReadOnlyList<DepositTrip> Trips, IReadOnlyList<ItemStack> PreMailedOverflow)`; `enum OverflowReason { NoChestAssigned, ChestFull, ChestMissing, NotDelivered }`; `record OverflowItem(ItemStack Stack, OverflowReason Reason)`. Pure Core. *S-10/S-11; domain-entities.md.*

**Step 3 — Create `DepositPlanner` (C-11, pure — Pattern M)**
[x] Create `Dayswork.Core/Inventory/IDepositPlanner.cs` + `DepositPlanner.cs`. `DepositPlan Plan(IReadOnlyList<BufferedItem> snapshot, IReadOnlyDictionary<TaskKind,DestinationKey> assignments, TileCoord shippingBinTile, TileCoord workerStart, Func<TileCoord,TileCoord,int> distance)`: resolve each item's destination (absent key or `MailDestination` → pre-mail `NoChestAssigned`); group+consolidate walkable items by destination (sum same item ids); nearest-neighbor order trips from `workerStart` via `distance`. Zero Stardew refs. *S-04/S-10; BR-OUT-02/03/04/08; PBT-U14-01..04.*

**Step 4 — Add `IntentDepositAtChest` (Pattern N)**
[x] Modify `Dayswork.Core/Shifts/ShiftIntent.cs`: add `sealed record IntentDepositAtChest(ChestRef Chest) : ShiftIntent;`. No state-machine table change (multi-trip via `SetIntent`). *S-10; BR-SM-02.*

**Step 5 — Extend `ShiftContext` (Pattern N/O)**
[x] Modify `Dayswork.Core/Shifts/ShiftContext.cs`: add `IReadOnlyDictionary<TaskKind,DestinationKey> TaskDestinations` (constructor param) and `List<OverflowItem> Overflow { get; } = new();`. *S-10/S-11; BR-OUT-02, BR-MAIL-01.*

**Step 6 — Core tests + shared generator (PBT)**
[x] Create `Dayswork.Tests/Generators/DepositInputGen.cs` — FsCheck generator producing `(IReadOnlyList<BufferedItem>, IReadOnlyDictionary<TaskKind,DestinationKey>)`, composing existing `TaskKind`/`ChestRef` gens (PBT-U14-05). Create `Dayswork.Tests/Inventory/DepositPlannerTests.cs` — properties: conservation (PBT-U14-01), trip-count = distinct walkable destinations (PBT-U14-02), no empty/mail trips (PBT-U14-03), resolution totality (PBT-U14-04), all with seed logging (PBT-U14-06); plus a nearest-neighbor sanity unit test. Update any existing `ItemBuffer` test for the new `Add` signature (conservation of `TakeAll`). *S-19; PBT-U14-01..06.*

### B. Mod — mail integration (Pattern P)

**Step 7 — Integrate with the installed MFM API**
[x] Initial generation created a guessed `IMailFrameworkModApi` stub; review-change Steps 17-19 replaced it with `Dayswork/Integration/MailFramework/MailFrameworkModApiAdapter.cs` after inspecting installed MFM 1.20.0. Dayswork now calls MFM's real `RegisterLetter(ILetter, ...)` API through reflection. *V9; TS-U14-03; DEV-U14-03.*

**Step 8 — Create `MailDispatcher` (M-16 — Pattern P)**
[x] Create `Dayswork/Integration/IMailDispatcher.cs` + `MailDispatcher.cs`. `QueueOverflowMail(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons)` → build a deliver-tomorrow MFM letter from sender `mail.sender`, body assembled by appending one i18n line per distinct reason (FD-Q6=A), with all items as attachments (one letter — S-11). `QueueToolMissingWarning(IReadOnlySet<TaskKind> tasks)` → vanilla `Game1.addMailForTomorrow(mailId)` no-item letter listing the task names (FD-Q7=A); "already sent" guarded via `mailReceived.Contains` (HashSet — TS-U14-05). Null MFM API → log + continue (REL-U14-05). All text via `I18nHelper`. *S-04/S-11; BR-MAIL-01..06.*

### C. Mod — orchestrator + composition root

**Step 9 — Modify `ShiftOrchestrator` (multi-trip deposit + mail — Patterns L/N/O)**
[x] Add fields `_chestResolver`, `_depositPlanner`, `_mailDispatcher` (constructor params).
[x] `StartShift`: thread `contract.TaskDestinations` into the new `ShiftContext`.
[x] Tag all buffering with the active task: pass `_pendingTask` into every `Buffer.Add(...)` and into `CollectNewDebris` (its internal `Buffer.Add`). *(Pattern L)*
[x] Replace `BeginDeposit` single shipping-bin trip with: build `DepositPlan` via `_depositPlanner.Plan(buffer.Snapshot(), ctx.TaskDestinations, ShippingBinTile, workerTile, Manhattan)`; seed `ctx.Overflow` from `PreMailedOverflow`; hold the trips as a queue; dispatch the first trip (`IntentDepositAtChest` or `IntentDepositInShippingBin`) and navigate. Zero trips → pass straight to Exiting (no bin walk). *(Pattern N)*
[x] Deposit handlers: `IntentDepositInShippingBin` → deposit all (no overflow); `IntentDepositAtChest` → on arrival `_chestResolver.ResolveChest(ref)`: null → items to `Overflow(ChestMissing)`; partial → deposit fit + remainder to `Overflow(ChestFull)`; full → deposit all. After each trip, `SetIntent` the next trip; when queue empty → `Transition(Exiting)`. *(Pattern N; S-10)*
[x] `HandleExit`: after refund, flush mail once — `Overflow` non-empty → `QueueOverflowMail`; `ToolMissingWarnings` non-empty → `QueueToolMissingWarning`. *(Pattern O; S-11)*
[x] `OnSaving`: replace the shipping-bin force-dump with: append remaining buffer to `Overflow(NotDelivered)`, flush the overflow letter, apply existing refund logic. *(FD-Q5=A; BR-INT-01)*
*S-04/S-10/S-11; all U-13B behaviour preserved (BR-PRESERVE-01).*

**Step 10 — Modify `ModEntry` (composition root)**
[x] Construct `DepositPlanner`; acquire the raw MFM API object via `Helper.ModRegistry.GetApi("DIGUS.MailFrameworkMod")` on `GameLoop.GameLaunched` and inject it into `MailDispatcher`; pass `chestResolver`, `depositPlanner`, `mailDispatcher`, and a Manhattan `Func<TileCoord,TileCoord,int>` oracle into `ShiftOrchestrator`. *S-10/S-11.*

### D. Config — manifest + i18n

**Step 11 — Modify `manifest.json` (MFM required dependency)**
[x] Add a `Dependencies` array with MFM (`{ "UniqueID": "DIGUS.MailFrameworkMod", "MinimumVersion": "<confirmed>" }`). Confirm exact UniqueID/min-version with Step 7. *COMPAT-U14-01; BR-DEP-01.*

**Step 12 — Modify `i18n/default.json` (mail strings)**
[x] Add keys: `mail.sender`, `mail.overflow.intro`, `mail.overflow.chest_full`, `mail.overflow.chest_missing`, `mail.overflow.no_chest_assigned`, `mail.overflow.not_delivered`, `mail.warning.tool_missing` (+ a task-name lookup the warning body enumerates). *UX-U14-01; NFR-UX-02.*

### E. Build, test, docs

**Step 13 — `dotnet build`**
[x] 0 errors / 0 warnings; mod auto-deploys to `Mods/Dayswork/`.

**Step 14 — `dotnet test`**
[x] New `DepositPlannerTests` green; **full regression green: 190 passed / 1 expected skip** (184 + 6 new planner tests; BR-PRESERVE-01).

**Step 15 — Create `aidlc-docs/construction/u-14-output-pipeline/code/code-summary.md`**
[x] Files created/modified; extension-compliance table; play-test checklist: (a) multi-task shift with 3 chests → 3 deposit trips; (b) chest full mid-deposit → remainder mailed; (c) chest destroyed mid-shift → its items mailed, others fine; (d) unassigned task output → mailed; (e) exactly one overflow letter, body lists each reason; (f) shipping-bin items never mailed; (g) tool-missing → separate vanilla letter; (h) sleep mid-deposit → items mailed (not dumped to bin); (i) refund unchanged; (j) U-10..U-13B scenarios regress clean; (k) **MFM behaviour verified in-game** (multi-item attachment delivers next morning).

**Step 16 — Update `aidlc-state.md` + `audit.md`**
[x] Mark U-14 Code Generation complete; append audit entry.

### F. Review change — installed Mail Framework Mod verification

**Step 17 — Inspect installed MFM 1.20.0 API**
[x] Read `X:\Steam\steamapps\common\Stardew Valley\Mods\MailFrameworkMod\manifest.json` and reflect `MailFrameworkMod.dll`. Confirmed UniqueID `DIGUS.MailFrameworkMod`, version `1.20.0`, and runtime API shape: `MailFrameworkMod.Api.IMailFrameworkModApi.RegisterLetter(ILetter, Func<ILetter,bool>, Action<ILetter>, Func<ILetter,List<Item>>)`.

**Step 18 — Replace guessed API stub with real MFM adapter**
[x] Delete the guessed `IMailFrameworkModApi.RegisterLetter(id, synopsis, text, attachments)` stub. Add `MailFrameworkModApiAdapter`, which fetches the raw MFM API object, creates MFM `Letter`/`ApiLetter` instances by reflection, sets title text, supplies a deliver-after-queued-day condition, and invokes MFM's real `RegisterLetter`. Update `MailDispatcher`, `ModEntry`, and `manifest.json` (`MinimumVersion: 1.20.0`).

**Step 19 — Verify MFM adapter change**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. Reflection smoke check created the adapter against installed MFM and reached `MailFrameworkMod.Api.MailFrameworkModApi.RegisterLetter`; the expected standalone failure was `Can't add a letter before the game is launched`, confirming binding reached MFM.

**Step 20 — Fix MFM repeated runtime letters**
[x] Inspect MFM IL for `MailRepository`, `MailController`, and `MailFrameworkModApi.RegisterLetter`. Confirmed API letters remain in MFM's repository until explicitly removed, and MFM invokes the registered callback when the letter menu closes. Updated `MailFrameworkModApiAdapter` to register a one-shot callback that removes the letter from MFM's repository after read/close, and added a `mailReceived` guard to the delivery condition.

**Step 21 — Fix empty/stale attachment boxes**
[x] Updated `MailFrameworkModApiAdapter` to register text-only MFM letters plus a `dynamicItems` delegate for item attachments. The delegate clones fresh item instances when the letter opens, avoiding stale static attachment objects and ensuring text-only warning letters provide no item list at all.

**Step 22 — Verify repeat/empty-box fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

**Step 23 — Investigate unexpected material inflation**
[x] Reviewed the worker item-ingress path after playtesting showed large material counts and copper ore from a small work area. Identified two unsafe sources: synthetic fallback material grants in `ShiftOrchestrator` and broad debris sweeps that could collect drops unrelated to the current task tile.

**Step 24 — Restrict worker output to real, task-local environment drops**
[x] Removed synthetic fallback `Buffer.Add(...)` material creation for rocks, trees, stumps/logs, and clumps. Replaced immediate debris collection with `CollectNewDebrisAtTile(...)` so only debris spawned near the worked tile enters the worker buffer; changed concrete item debris capture to use `QualifiedItemId`; tightened no-position debris so it cannot bypass the near-tile check.

**Step 25 — Verify material-source fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

**Step 26 — Reopen material inflation after failed playtest**
[x] User playtest still showed copper ore and oversized wood stacks after Step 24. Reflected/decompiled Stardew `Debris.collect` and `Debris.InitializeResource`; confirmed vanilla awards one item per resource debris object and stores the collectible ID in `Debris.itemId`.

**Step 27 — Stop inferring materials from debris visual chunks**
[x] Removed the `chunkType -> itemId` inference and stopped using `Debris.Chunks.Count` as stack quantity. `TryGetDebrisItem` now accepts only `debris.item` (with its real stack) or explicit `debris.itemId` (stack 1, matching vanilla collect semantics). Added a debug log for each collected debris item with task, item ID, chunk count, debris type, and chunk type for future playtest diagnosis.

**Step 28 — Verify debris-semantics fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

**Step 29 — Investigate tree wood under-collection**
[x] User playtest showed a single fully grown standard tree produced only 8 wood. Reviewed Dayswork's tree action lifecycle and confirmed the likely path: the trunk-fall wood is delayed by Stardew's falling-tree animation, while Dayswork immediately chops the stump, marks the tile complete, and begins deposit planning.

**Step 30 — Wait for delayed tree debris before final deposit**
[x] Added a pre-deposit wait for pending debris sweeps. When the last work item completes but tree debris sweeps are still active, the worker pauses deposit planning until those sweeps finish collecting trunk-fall debris, then starts the normal deposit route. This prevents `BeginDeposit` from flushing and clearing a pending sweep before the trunk wood exists.

**Step 31 — Verify tree-delay fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

**Step 32 — Reopen tree wood under-collection after failed playtest**
[x] User playtest still showed only 8 wood from one tree. Inspected vanilla `Debris.collect`, `Debris.updateChunks`, `Tree.performToolAction`, and `Tree.performTreeFall`. Confirmed resource debris with explicit `itemId` is collected one chunk at a time; each `Chunk` is one item for `DebrisType.RESOURCE`.

**Step 33 — Count explicit resource debris chunks, not unlabeled visual chunks**
[x] Updated `TryGetDebrisItem` so explicit `debris.itemId` with `DebrisType.RESOURCE` uses `debris.Chunks.Count` as quantity, matching vanilla per-chunk collection. The unsafe path remains blocked: Dayswork still does not infer material identity from unlabeled `chunkType` values, so color/type-only debris cannot create foreign materials.

**Step 34 — Verify resource-chunk quantity fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork` (rerun with approved `dotnet build` permission after sandboxed NuGet config access failed). `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

**Step 35 — Investigate rock output and empty mail after tree fix**
[x] User playtest confirmed tree output collected 17 wood, but standard rock clearing removed the object without adding stone, and an empty worker letter still appeared. Inspected Stardew `Object.performToolAction` IL for breakable objects and confirmed the standard rock branch creates radial visual chunk debris without an explicit collectible item id, so Dayswork's no-foreign-material filter correctly rejected it.

**Step 36 — Restore standard stone drops without broad material inference**
[x] Added a narrow removed-object conversion only for standard Stone objects: if a ClearRocks action removed the object and no explicit item-bearing debris was collected, buffer exactly 1 `(O)390` Stone for that actual removed environment object. The unsafe path remains blocked: Dayswork still never maps unlabeled visual `chunkType` values to copper/wood/stone, so unrelated or color-only debris cannot create materials. Added mail debug logs and changed MFM registration to always provide a `dynamicItems` callback, including no-attachment warning letters, to diagnose and reduce empty reward-box behavior.

**Step 37 — Verify rock/mail diagnostic fix**
[x] `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> 190 passed / 1 expected skip. In-game verification pending user playtest.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-04 orphan chest → mail (completes) | 3, 8, 9 |
| S-10 multi-trip deposit + fallbacks + refund (completes) | 1–5, 9, 10 |
| S-11 overflow mail (completes) | 2, 5, 8, 9, 11, 12 |
| S-19 PBT obligations | 6 |

## Scope summary
**37 steps**: 16 original generation steps plus 21 review/playtest-fix steps. New files include `DepositPlan.cs`, `IDepositPlanner.cs`/`DepositPlanner.cs`, `IMailDispatcher.cs`/`MailDispatcher.cs`, `MailFrameworkModApiAdapter.cs`, planner tests + generator. The guessed `IMailFrameworkModApi.cs` file was deleted after local MFM inspection. One real external integration (MFM) — exact API confirmed from installed MFM 1.20.0. Current play-test focus: one-shot mail delivery, attachment rendering, verifying the worker only buffers materials from actual task-local environment drops/objects, confirming tree drops include both trunk and stump wood with vanilla per-resource-chunk quantities, and confirming a cleared standard rock yields stone without reintroducing foreign materials.
