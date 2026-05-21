# U-14 — NFR Design Patterns

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail

U-14 changes only the **deposit/mail seam**. The entire worker behavioural loop from U-10/U-13/U-13B is retained unchanged. New patterns L–P cover task-tagged buffering, pure deposit planning, the multi-trip loop, overflow accumulation, and the MFM mail adapter.

---

## Retained unchanged (from U-10/U-13/U-13B)

- **Throttled-Tick** (÷4 dispatch), **Manual Path-Follow Movement** (Pattern G), **Farmer-as-Worker Rendering** (Pattern F), **Save-Exclusion Guard** (Pattern I), **Progress-Sampling Stuck Detection** + **3-Step Escalation** (D/E), **Inherent Invulnerability + Swing Emote** (H), **Pure Tool Map + Mod Swing** (K), **Once-Per-Shift Scan**, **Invoke-and-Poll** task effects, **Core-Purity Guard**. None of these is touched (guarded by BR-PRESERVE-01 + the green U-13/U-13B test suites).
- The refund/exit path (refund applied at entrance exit, integer-clamped) is unchanged (BR-OUT-11 / SAFE-U14-02).

---

## Pattern L — Collection-Time Task Tagging
**Satisfies**: FD-Q1=A, BR-OUT-01, SAFE-U14-01, MAINT-U14-01

The point where the worker buffers a self-caused drop already knows the active task (`_pendingTask`). That `TaskKind` is recorded **with** the item: `ItemBuffer.Add(qualifiedItemId, qty, sourceTask)`. The buffer stays a pure-Core record (now `BufferedItem { QualifiedItemId, Quantity, SourceTask }`); it does **not** resolve destinations or touch chests. Destination resolution is deferred entirely to the planner (Pattern M) so all routing logic lives in one pure, testable place and so chest *liveness* is never frozen at collection time. The existing `CollectNewDebris` snapshot-diff (SAFE-U14-04) is unchanged — only the `Add` signature gains the task.

---

## Pattern M — Pure Deposit Planner with Injected Distance Oracle
**Satisfies**: FD-Q1/Q2/Q3=A, BR-OUT-02/03/04/08, SAFE-U14-01, PERF-U14-01, MAINT-U14-01, PBT-U14-01..05

`C-11 DepositPlanner` is pure Core (zero Stardew refs). `Plan(snapshot, taskDestinations, shippingBinTile, workerStart, distance)`:

1. **Resolve** each `BufferedItem` via `taskDestinations[SourceTask]`: chest/bin → walkable; absent-key or `MailDestination` → `PreMailedOverflow` (reason `NoChestAssigned`) (FD-Q2=A).
2. **Group + consolidate** walkable items by destination (one trip per distinct chest + the bin; same item ids summed).
3. **Order** trips nearest-neighbor from `workerStart` using the injected `Func<TileCoord,TileCoord,int>` oracle — **Manhattan in v1**, matching U-13's nearest-task routing (DEV-02). Game pathfinding never enters Core.

Returns `DepositPlan { Trips, PreMailedOverflow }`. **Conservation** (items in = items across trips ∪ pre-mail) and **trip-count = distinct walkable destinations** are the planner's PBT invariants (PBT-U14-01/02/03/04). Cost is O(n) resolve + O(d²) order over a tiny `d`, run **once** at shift end (PERF-U14-01) — never per frame.

---

## Pattern N — Multi-Trip Deposit Loop (intent re-issue, no new phase)
**Satisfies**: BR-SM-01/02, FR-WORK-05/06, SAFE-U14-01, REL-U14-01/02

The state machine table is **unchanged** (no new phases; PBT invariants preserved). The orchestrator holds the plan's trips as a queue and seeds `ShiftContext.Overflow` with `PreMailedOverflow`. Within the `Depositing` phase:

- On entering Depositing, dispatch the **first** trip via the state machine's intent (`IntentDepositAtChest(chestRef)` for a chest, `IntentDepositInShippingBin` for the bin) and navigate to its tile.
- On each Depositing dispatch tick, the current trip is executed on arrival; then the next trip is **re-issued via `SetIntent`** (legal within an active phase) — no transition needed between trips.
- When the queue empties, transition `Depositing → Exiting`.
- **Zero-trip case** (everything mail-bound, or empty buffer): Depositing is a one-tick pass-through — it is entered and, finding no trip to walk, transitions straight to `Exiting` with no wasteful walk to the bin. (Exiting is still reached only via Depositing, so the table/PBT invariants hold.)

Chest execution resolves the live chest at **arrival** via `ChestResolver.ResolveChest` (Pattern P consumer): null → items to Overflow (`ChestMissing`); partial fit → deposit what fits, remainder to Overflow (`ChestFull`); full fit → deposit all. All trips run to completion even past the 8pm cap (FR-WORK-06): the cap ends *working*, not *depositing*.

---

## Pattern O — Overflow Accumulator + Single-Letter Flush
**Satisfies**: FD-Q5/Q6=A, BR-MAIL-01/02/03/04, BR-INT-01, SAFE-U14-01, REL-U14-03

`ShiftContext.Overflow : List<OverflowItem>` is the single sink for everything undeliverable:
- seeded with `PreMailedOverflow` (`NoChestAssigned`),
- appended during the deposit loop on chest-full (`ChestFull`) / chest-missing (`ChestMissing`),
- on `OnSaving` mid-deposit, the **entire remaining buffer** is appended with reason `NotDelivered` and **nothing is force-dumped into the shipping bin** (FD-Q5=A) — the prior "dump to bin" behaviour is removed.

At shift end (exit, or the save-interrupt path), the accumulator is **flushed exactly once**: if non-empty, `MailDispatcher.QueueOverflowMail(unionItems, distinctReasons)` produces a single letter carrying all items (S-11) with a body that conditionally lists each distinct reason present (FD-Q6=A). Empty accumulator → no letter. Shipping-bin items can never appear here (they never overflow — FR-OUT-06).

---

## Pattern P — Mail Adapter over MFM (deliver-tomorrow) + vanilla no-item warnings
**Satisfies**: FD-Q4/Q7=A, V9, BR-MAIL-01/05/06, BR-DEP-01, COMPAT-U14-01, SAFE-U14-03, REL-U14-04/05, NFR-MAINT-04

`M-16 MailDispatcher` is a thin adapter behind `IMailDispatcher`:

- **Acquisition**: the vendored MFM API stub is resolved once at startup via `Helper.ModRegistry.GetApi<...>("DIGUS.MailFrameworkMod")` (UniqueID/min-version confirmed at code-gen). MFM is a required dependency (manifest `Dependencies`), so this is expected to succeed.
- **Overflow letter** → MFM multi-attachment letter queued **for tomorrow** (FD-Q4=A); Stardew/MFM owns persistence — **no custom Dayswork save data** (SAFE-U14-03). Sender label "Your farmhand", body, and per-task names all read from `I18nHelper` (UX-U14-01).
- **Tool-missing warning** → a **separate** vanilla `Game1.addMailForTomorrow(mailId)` no-item letter listing all `ToolMissingWarnings` task kinds (FD-Q7=A). Any "already-sent" check uses `mailReceived.Contains` (HashSet in 1.6 — TS-U14-05).
- **No new Harmony patches** (NFR-MAINT-04): mail goes through the MFM API, not a draw/letter patch.

**Resilience resolutions (the two NFR-Requirements deferrals):**
- **Large attachment (REL-U14-04)**: the full overflow set is handed to MFM in one call; the product rule "one letter, all items" (S-11) is non-negotiable, so if a practical MFM per-letter cap is ever hit, that surfaces as a code-gen play-test finding rather than silent truncation.
- **Null/unavailable API (REL-U14-05)**: log an error and complete the shift without crashing; because items live in the buffer/overflow intent (never deleted before a confirmed send), item-safety is preserved even on dispatch failure.

`IMailDispatcher` is shaped so U-15 can add `QueueCannotAffordMail(...)` without reshaping the adapter.

---

## Resilience Assessment

| Failure scenario | Handling | Pattern |
|---|---|---|
| Task has no assigned chest | Resolved to mail at planning | M / O |
| Assigned chest full at arrival | Deposit what fits; remainder → mail (`ChestFull`) | N / O |
| Assigned chest moved/destroyed | Items → mail (`ChestMissing`); other trips continue | N / O |
| 8pm cap reached mid-shift | All deposit trips still complete | N |
| Player sleeps/saves mid-deposit | Remaining buffer → mail (`NotDelivered`); no bin dump | O / BR-INT-01 |
| No walkable destinations / empty buffer | Depositing pass-through straight to Exiting | N |
| Overflow item set very large | One MFM call, all items; cap is a play-test finding | P / REL-U14-04 |
| MFM API unavailable at runtime | Log + continue; items never pre-deleted | P / REL-U14-05 |
| Any U-13/U-13B behavioural regression | Guarded by BR-PRESERVE-01 + green Core suites | retained |

## Scalability Assessment
N/A — single-player mod; destination counts are tiny (a handful per shift).

## Security Assessment
N/A — Security Baseline extension disabled (Requirements Analysis Q28). No network, PII, auth, or external-input surface.
