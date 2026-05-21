# U-14 — Output Pipeline: Code Generation Plan

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stories**: S-04 (completes — orphan chest → mail), S-10 (completes — multi-trip deposit + chest-full/missing fallbacks + refund), S-11 (completes — overflow mail), S-19 (PBT obligations)
**Phase**: CONSTRUCTION — Code Generation (Part 1: Planning)

> This plan is the single source of truth for U-14 Code Generation. Generation (Part 2) executes these steps in order **after approval**.

---

## Unit Context

**Components owned (new files)**: C-11 `DepositPlanner` (Core); M-16 `MailDispatcher` (Mod); the deposit/overflow domain types (Core); the vendored MFM API stub (Mod).
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
[ ] Modify `Dayswork.Core/Inventory/IItemBuffer.cs` + `ItemBuffer.cs`. Add `record BufferedItem(string QualifiedItemId, int Quantity, TaskKind SourceTask)` (in `Dayswork.Core/Inventory/`). Change `Add(string itemId, int quantity, TaskKind sourceTask)`; `Snapshot()` / `TakeAll()` return `IReadOnlyList<BufferedItem>`. Keep validation (non-empty id, qty>0). Pure Core. *S-10; BR-OUT-01; TS-U14-06.*

**Step 2 — Create deposit/overflow domain types**
[ ] Create `Dayswork.Core/Inventory/DepositPlan.cs` containing: `record ItemStack(string QualifiedItemId, int Quantity)`; `record DepositTrip(DestinationKey Destination, TileCoord Tile, IReadOnlyList<ItemStack> Items)`; `record DepositPlan(IReadOnlyList<DepositTrip> Trips, IReadOnlyList<ItemStack> PreMailedOverflow)`; `enum OverflowReason { NoChestAssigned, ChestFull, ChestMissing, NotDelivered }`; `record OverflowItem(ItemStack Stack, OverflowReason Reason)`. Pure Core. *S-10/S-11; domain-entities.md.*

**Step 3 — Create `DepositPlanner` (C-11, pure — Pattern M)**
[ ] Create `Dayswork.Core/Inventory/IDepositPlanner.cs` + `DepositPlanner.cs`. `DepositPlan Plan(IReadOnlyList<BufferedItem> snapshot, IReadOnlyDictionary<TaskKind,DestinationKey> assignments, TileCoord shippingBinTile, TileCoord workerStart, Func<TileCoord,TileCoord,int> distance)`: resolve each item's destination (absent key or `MailDestination` → pre-mail `NoChestAssigned`); group+consolidate walkable items by destination (sum same item ids); nearest-neighbor order trips from `workerStart` via `distance`. Zero Stardew refs. *S-04/S-10; BR-OUT-02/03/04/08; PBT-U14-01..04.*

**Step 4 — Add `IntentDepositAtChest` (Pattern N)**
[ ] Modify `Dayswork.Core/Shifts/ShiftIntent.cs`: add `sealed record IntentDepositAtChest(ChestRef Chest) : ShiftIntent;`. No state-machine table change (multi-trip via `SetIntent`). *S-10; BR-SM-02.*

**Step 5 — Extend `ShiftContext` (Pattern N/O)**
[ ] Modify `Dayswork.Core/Shifts/ShiftContext.cs`: add `IReadOnlyDictionary<TaskKind,DestinationKey> TaskDestinations` (constructor param) and `List<OverflowItem> Overflow { get; } = new();`. *S-10/S-11; BR-OUT-02, BR-MAIL-01.*

**Step 6 — Core tests + shared generator (PBT)**
[ ] Create `Dayswork.Tests/Generators/DepositInputGen.cs` — FsCheck generator producing `(IReadOnlyList<BufferedItem>, IReadOnlyDictionary<TaskKind,DestinationKey>)`, composing existing `TaskKind`/`ChestRef` gens (PBT-U14-05). Create `Dayswork.Tests/Inventory/DepositPlannerTests.cs` — properties: conservation (PBT-U14-01), trip-count = distinct walkable destinations (PBT-U14-02), no empty/mail trips (PBT-U14-03), resolution totality (PBT-U14-04), all with seed logging (PBT-U14-06); plus a nearest-neighbor sanity unit test. Update any existing `ItemBuffer` test for the new `Add` signature (conservation of `TakeAll`). *S-19; PBT-U14-01..06.*

### B. Mod — mail integration (Pattern P)

**Step 7 — Vendor the MFM API stub**
[ ] Create `Dayswork/Integration/MailFramework/IMailFrameworkModApi.cs` — the minimal MFM interface Dayswork calls for a runtime multi-item letter (same vendor-the-API pattern as the planned GMCM stub). **Code-gen verification point:** confirm MFM's exact UniqueID (`DIGUS.MailFrameworkMod`), interface name, and the add-letter method signature against the published MFM API before finalizing; adjust the stub to match. *V9; TS-U14-03.*

**Step 8 — Create `MailDispatcher` (M-16 — Pattern P)**
[ ] Create `Dayswork/Integration/IMailDispatcher.cs` + `MailDispatcher.cs`. `QueueOverflowMail(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons)` → build a deliver-tomorrow MFM letter from sender `mail.sender`, body assembled by appending one i18n line per distinct reason (FD-Q6=A), with all items as attachments (one letter — S-11). `QueueToolMissingWarning(IReadOnlySet<TaskKind> tasks)` → vanilla `Game1.addMailForTomorrow(mailId)` no-item letter listing the task names (FD-Q7=A); "already sent" guarded via `mailReceived.Contains` (HashSet — TS-U14-05). Null MFM API → log + continue (REL-U14-05). All text via `I18nHelper`. *S-04/S-11; BR-MAIL-01..06.*

### C. Mod — orchestrator + composition root

**Step 9 — Modify `ShiftOrchestrator` (multi-trip deposit + mail — Patterns L/N/O)**
[ ] Add fields `_chestResolver`, `_depositPlanner`, `_mailDispatcher` (constructor params).
[ ] `StartShift`: thread `contract.TaskDestinations` into the new `ShiftContext`.
[ ] Tag all buffering with the active task: pass `_pendingTask` into every `Buffer.Add(...)` (lines ~680/695/716/731) and into `CollectNewDebris` (its internal `Buffer.Add` at ~761). *(Pattern L)*
[ ] Replace `BeginDeposit` single shipping-bin trip with: build `DepositPlan` via `_depositPlanner.Plan(buffer.Snapshot(), ctx.TaskDestinations, ShippingBinTile, workerTile, Manhattan)`; seed `ctx.Overflow` from `PreMailedOverflow`; hold the trips as a queue; dispatch the first trip (`IntentDepositAtChest` or `IntentDepositInShippingBin`) and navigate. Zero trips → pass straight to Exiting (no bin walk). *(Pattern N)*
[ ] Deposit handlers: `IntentDepositInShippingBin` → deposit all (no overflow); `IntentDepositAtChest` → on arrival `_chestResolver.ResolveChest(ref)`: null → items to `Overflow(ChestMissing)`; partial → deposit fit + remainder to `Overflow(ChestFull)`; full → deposit all. After each trip, `SetIntent` the next trip; when queue empty → `Transition(Exiting)`. *(Pattern N; S-10)*
[ ] `HandleExit`: after refund, flush mail once — `Overflow` non-empty → `QueueOverflowMail`; `ToolMissingWarnings` non-empty → `QueueToolMissingWarning`. *(Pattern O; S-11)*
[ ] `OnSaving`: replace the shipping-bin force-dump with: append remaining buffer to `Overflow(NotDelivered)`, flush the overflow letter, apply existing refund logic. *(FD-Q5=A; BR-INT-01)*
*S-04/S-10/S-11; all U-13B behaviour preserved (BR-PRESERVE-01).*

**Step 10 — Modify `ModEntry` (composition root)**
[ ] Construct `DepositPlanner`; acquire MFM API via `Helper.ModRegistry.GetApi<IMailFrameworkModApi>("DIGUS.MailFrameworkMod")` and construct `MailDispatcher`; pass `chestResolver`, `depositPlanner`, `mailDispatcher`, and a Manhattan `Func<TileCoord,TileCoord,int>` oracle into `ShiftOrchestrator`. *S-10/S-11.*

### D. Config — manifest + i18n

**Step 11 — Modify `manifest.json` (MFM required dependency)**
[ ] Add a `Dependencies` array with MFM (`{ "UniqueID": "DIGUS.MailFrameworkMod", "MinimumVersion": "<confirmed>" }`). Confirm exact UniqueID/min-version with Step 7. *COMPAT-U14-01; BR-DEP-01.*

**Step 12 — Modify `i18n/default.json` (mail strings)**
[ ] Add keys: `mail.sender`, `mail.overflow.intro`, `mail.overflow.chest_full`, `mail.overflow.chest_missing`, `mail.overflow.no_chest_assigned`, `mail.overflow.not_delivered`, `mail.warning.tool_missing` (+ a task-name lookup the warning body enumerates). *UX-U14-01; NFR-UX-02.*

### E. Build, test, docs

**Step 13 — `dotnet build`**
[ ] 0 errors / 0 warnings; mod auto-deploys to `Mods/Dayswork/`.

**Step 14 — `dotnet test`**
[ ] New `DepositPlannerTests` green; **full U-13B regression suite (184) still green** (BR-PRESERVE-01).

**Step 15 — Create `aidlc-docs/construction/u-14-output-pipeline/code/code-summary.md`**
[ ] Files created/modified; extension-compliance table; play-test checklist: (a) multi-task shift with 3 chests → 3 deposit trips; (b) chest full mid-deposit → remainder mailed; (c) chest destroyed mid-shift → its items mailed, others fine; (d) unassigned task output → mailed; (e) exactly one overflow letter, body lists each reason; (f) shipping-bin items never mailed; (g) tool-missing → separate vanilla letter; (h) sleep mid-deposit → items mailed (not dumped to bin); (i) refund unchanged; (j) U-10..U-13B scenarios regress clean; (k) **MFM behaviour verified in-game** (multi-item attachment delivers next morning).

**Step 16 — Update `aidlc-state.md` + `audit.md`**
[ ] Mark U-14 Code Generation complete; append audit entry.

---

## Story Traceability

| Story | Steps |
|---|---|
| S-04 orphan chest → mail (completes) | 3, 8, 9 |
| S-10 multi-trip deposit + fallbacks + refund (completes) | 1–5, 9, 10 |
| S-11 overflow mail (completes) | 2, 5, 8, 9, 11, 12 |
| S-19 PBT obligations | 6 |

## Scope summary
**16 steps**: 6 Core (1 buffer extend, 1 types, 1 planner, 1 intent, 1 context, 1 tests+generator) + 4 Mod (MFM stub, MailDispatcher, orchestrator, ModEntry) + 2 config (manifest, i18n) + build/test + docs/state. New files: `DepositPlan.cs`, `IDepositPlanner.cs`/`DepositPlanner.cs`, `IMailDispatcher.cs`/`MailDispatcher.cs`, `IMailFrameworkModApi.cs`, planner tests + generator. No file deletions. One real external integration (MFM) — its exact API confirmed at Steps 7/11 and verified in-game at Step 15.
