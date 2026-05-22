# U-14 Code Summary — Output Pipeline (Multi-Destination Deposit + Overflow Mail)

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Status**: Code generation complete; MFM integration reviewed against installed Mail Framework Mod 1.20.0; build and automated tests pass.
**Verification**: `dotnet build Dayswork.sln` -> 0 errors / 0 warnings, auto-deployed to `Mods/Dayswork`. `dotnet test Dayswork.sln` -> **190 passed, 1 expected skip** (184 prior + 6 new planner tests). Reflection smoke check reached MFM's real `RegisterLetter` method and failed only because it was run outside the launched game.
**Stories**: S-04 (orphan chest → mail), S-10 (multi-trip deposit + chest-full/missing fallbacks + refund), S-11 (overflow mail), S-19 (PBT obligations).

## What changed

The worker now routes collected items per task instead of dumping everything in the shipping bin:

- **Collection-time tagging (Pattern L)** — every drop is buffered with the `TaskKind` that produced it.
- **Pure planner (Pattern M)** — `DepositPlanner` resolves each item's destination via the contract's `TaskDestinations` (absent ⇒ mail, FD-Q2=A), consolidates by destination, and orders trips nearest-neighbor using an injected Manhattan oracle.
- **Multi-trip deposit loop (Pattern N)** — the `Depositing` phase drives one intent per trip via `SetIntent` (new `IntentDepositAtChest`), with **no new state-machine phase**. Chest liveness is resolved on arrival: missing → mail (`ChestMissing`), full → deposit-what-fits + mail remainder (`ChestFull`). Shipping bin never overflows. Zero-trip is a one-tick pass-through to Exiting.
- **Overflow accumulator + single-letter flush (Pattern O)** — undeliverable items collect in `ShiftContext.Overflow`; at exit one no-fee MFM letter carries them all, body listing each distinct reason (FD-Q6=A). Sleeping mid-cleanup mails leftovers (`NotDelivered`) instead of dumping to the bin (FD-Q5=A).
- **Mail adapter (Pattern P)** — `MailDispatcher` over the confirmed MFM 1.20.0 API; tool-missing warning is a separate text-only letter.

Refund/exit math is unchanged (FR-PAY-05). The whole U-13B worker loop (movement, render, stuck, invuln, tool visuals) is preserved.

## Created

| File | Purpose |
|---|---|
| `Dayswork.Core/Inventory/BufferedItem.cs` | Buffered drop tagged with `SourceTask` (Pattern L). |
| `Dayswork.Core/Inventory/DepositPlan.cs` | `ItemStack`, `DepositTrip`, `DepositPlan`, `OverflowReason`, `OverflowItem`. |
| `Dayswork.Core/Inventory/IDepositPlanner.cs` + `DepositPlanner.cs` | C-11 pure planner (Pattern M). |
| `Dayswork/Integration/MailFramework/MailFrameworkModApiAdapter.cs` | Reflection adapter for installed MFM 1.20.0 API (`Letter`/`ApiLetter` + `RegisterLetter(ILetter, ...)`) without bundling or compile-referencing MFM. |
| `Dayswork/Integration/IMailDispatcher.cs` + `MailDispatcher.cs` | M-16 mail adapter (Pattern P). |
| `Dayswork.Tests/Generators/DepositInputGen.cs` | PBT-U14-05 shared generator for planner inputs. |
| `Dayswork.Tests/Inventory/DepositPlannerTests.cs` | PBT-U14-01..04 (conservation, trip-count, no empty/mail trips, totality) + nearest-first + unassigned-→-mail facts. |

## Modified

| File | Change |
|---|---|
| `Dayswork.Core/Inventory/IItemBuffer.cs` + `ItemBuffer.cs` | `Add(itemId, qty, sourceTask)`; `Snapshot`/`TakeAll` return `IReadOnlyList<BufferedItem>` (C-10 extension; deviation from matrix). |
| `Dayswork.Core/Shifts/ShiftIntent.cs` | Added `IntentDepositAtChest(ChestRef)`. |
| `Dayswork.Core/Shifts/ShiftContext.cs` | Added `TaskDestinations` (ctor) and `Overflow` list. |
| `Dayswork/Orchestration/ShiftOrchestrator.cs` | Thread `TaskDestinations`; tag every buffer write with `_pendingTask`; planner-driven multi-trip deposit loop with chest resolve/full/missing → overflow; mail flush at exit; `OnSaving` mails leftovers (no bin dump). |
| `Dayswork/ModEntry.cs` | Construct `DepositPlanner` + `MailDispatcher`; wire `chestResolver`/planner/dispatcher into `ShiftOrchestrator`. **Raw MFM API object injected on `GameLoop.GameLaunched`** (mod-provided APIs must not be fetched in `Entry()`). |
| `Dayswork/manifest.json` | MFM (`DIGUS.MailFrameworkMod`) added as a required dependency with `MinimumVersion` `1.20.0`. |
| `Dayswork/i18n/default.json` | `mail.sender`, `mail.overflow.*`, `mail.warning.tool_missing`. |
| `Dayswork.Tests/Generators/ItemBufferGen.cs`, `Inventory/ItemBufferTests.cs` | Updated for the new `Add` signature / `BufferedItem` shape. |

## Deviations recorded

- **DEV-U14-01 — Tool-missing warning via MFM, not vanilla mail.** FD-Q7=A / plan Step 8 specified vanilla `addMailForTomorrow`. Implemented through MFM as a text-only (no-attachment) letter because vanilla custom mail can't cleanly carry per-shift dynamic text (the skipped-task list) and re-deliver daily. FD-Q7=A's behavioural intent (one separate combined no-item warning per shift) is preserved.
- **DEV-U14-02 — `OnSaving` mid-work branch mails collected items.** Previously discarded in-progress items on a genuine mid-work save; now mails them (NFR-SAFE-01) while keeping the existing full-deposit refund. Proper sleep fast-forward (billing nuance) remains U-15.
- **C-10 ItemBuffer extended** despite the component matrix listing it as not-extended (required by FD-Q1=A; recorded in domain-entities.md).

## MFM review result

- **Confirmed installed MFM**: `DIGUS.MailFrameworkMod`, version `1.20.0`.
- **Confirmed API shape**: `RegisterLetter(ILetter, Func<ILetter,bool>, Action<ILetter>, Func<ILetter,List<Item>>)`.
- **Implemented adapter**: Dayswork now fetches the raw MFM API object with `GetApi("DIGUS.MailFrameworkMod")`, creates MFM `Letter` and `ApiLetter` instances by reflection, and passes a condition that only becomes true after the in-game day when the letter was queued. This preserves deliver-tomorrow behavior even if MFM refreshes the mailbox during the same day.
- **Fallback remains**: if MFM binding or registration fails in-game, overflow items still fall back to the shipping bin and tool warnings log, so no items are lost.

## Post-deploy fix

- **MFM API fetched on `GameLaunched`, not `Entry()`.** Initial play-test surfaced the SMAPI warning *"Tried to access a mod-provided API before all mods were initialized."* `MailDispatcher` is now constructed without the API in `Entry()` and the MFM API is injected via `MailDispatcher.SetApi(...)` from a `GameLoop.GameLaunched` handler. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **MFM API signature corrected after local inspection.** The guessed `IMailFrameworkModApi.RegisterLetter(id, synopsis, text, attachments)` stub was removed and replaced with the reflection adapter above. `manifest.json` now requires MFM `1.20.0`.
- **Repeated mail + empty box fix.** Playtesting showed MFM API letters remained valid after collection and were delivered again on later days, and item slots could appear empty/stale. The adapter now registers a callback that removes the runtime letter from MFM's repository when the letter closes, adds a `mailReceived` guard to the delivery condition, and supplies item rewards through MFM's `dynamicItems` callback so each open receives fresh item instances. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **No synthetic or foreign materials.** Playtesting showed inflated material counts and copper ore from a small work area. `ShiftOrchestrator` no longer grants fallback wood/stone/hardwood when a tool action succeeds or removes a target; all worker inventory now comes from real `Debris` produced by the game. Immediate debris sweeps are bound to the worked tile, concrete item debris uses `QualifiedItemId`, and no-position debris cannot bypass the near-tile filter. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **Debris quantity semantics corrected.** Follow-up playtesting still showed oversized stacks. Reflection/IL inspection confirmed vanilla `Debris.collect` awards one item per resource debris object and uses `Debris.itemId` as the collectible ID; `Debris.Chunks` are visual particles, not quantity. `TryGetDebrisItem` now accepts only real `debris.item` stacks or explicit `debris.itemId` resource drops (quantity 1), and no longer maps `chunkType` to materials. A debug log records each accepted debris item for future playtest diagnosis. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **Delayed tree-fall wood preserved.** Playtesting showed a fully grown tree only deposited 8 wood, consistent with stump-only collection. The worker now waits for pending tree debris sweeps to finish before building the final deposit plan, so delayed trunk-fall wood has time to enter the buffer before the worker walks to the chest/bin. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **Resource chunk quantities matched to vanilla.** Follow-up playtesting still showed only 8 wood. IL inspection of `Debris.collect`/`updateChunks` confirmed explicit `DebrisType.RESOURCE` debris is collected one chunk at a time. `TryGetDebrisItem` now counts `Chunks.Count` only for explicit `debris.itemId` resource debris, while still refusing unlabeled `chunkType` inference. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.
- **Standard rock drops restored without foreign material inference.** Follow-up playtesting showed tree output fixed but standard rocks removed without stone. IL inspection confirmed the standard object break path creates radial visual chunk debris with no item id. Dayswork now converts only a removed standard Stone object into exactly 1 `(O)390` Stone when no item-bearing debris was collected; it still refuses unlabeled visual chunks, so copper/wood/ore cannot appear unless Stardew supplied an explicit item. Mail dispatch now logs queued/registering letter counts and always supplies MFM `dynamicItems`, even for no-attachment letters, to diagnose the remaining empty-mail report. `dotnet build` 0/0; `dotnet test` 190 passed / 1 skip.

## Extension compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled in `aidlc-state.md` for this project; skipped for this review. |
| Property-Based Testing (partial) | Compliant | The adapter change does not add a pure planning/serialization algorithm requiring new PBT. Existing FsCheck planner properties still pass; PBT-08/PBT-09 remain satisfied. |

## Play-test checklist

- Multi-task shift with 3 assigned chests → 3 deposit trips (nearest-first), each chest gets its task's items.
- Chest full mid-deposit → remainder arrives by mail; chest destroyed mid-shift → its items mail, other trips proceed.
- Output task left unassigned → its drops mail (not shipping bin).
- Exactly one overflow letter, body lists each reason that applied; shipping-bin items never mailed.
- Tool-gated tasks → one separate warning letter listing them.
- Sleep mid-deposit → items mailed next morning (not dumped to bin); refund correct.
- `dotnet test` green incl. planner conservation/trip-count properties; U-10..U-13B scenarios regress clean.
- **MFM**: with MFM 1.20.0 installed, the overflow letter arrives next morning carrying all items as attachments; after collection/read it should not repeat on later days.
- **Material source**: a tiny selected work area should only deposit/mail drops actually spawned by cleared objects in that area; no copper ore or oversized wood/fiber stacks should appear unless the environment produced them.
- **Tree drops**: one fully grown standard tree should produce trunk wood plus stump wood, not only the 5-9 stump range.
- **Rock drops**: one cleared standard stone should add 1 Stone and should log `[Dayswork][debris] collected 1x (O)390 from removed standard stone object`; no copper should appear unless an actual ore node was cleared.
- **Empty mail diagnosis**: if an empty worker letter still appears, capture the new `[Dayswork][mail]` lines showing whether it was an overflow letter or a tool-warning letter and how many attachments MFM registered.
