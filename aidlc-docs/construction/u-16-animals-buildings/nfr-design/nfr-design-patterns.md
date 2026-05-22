# U-16 — NFR Design Patterns

**Unit**: U-16 — Animals & Buildings

NFR design decisions applied: NFR-DES-Q1=A (constructor-injected seams), NFR-DES-Q2=A (LogLevel.Warn for building-skip), NFR-DES-Q3=A (IndoorWorkScanner stateless, caller owns result). Builds on approved NFR Requirements (SAFE/PERF/REL/UX/MAINT/COMPAT/PBT-U16-01..06) and FD decisions (FD-Q1=A…Q9=A).

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — disabled project-wide (NFR-SEC-01; no network/PII/auth surface) |
| Scalability / HA | **N/A** — single-player local game process; bounded by game design (≤ 12 animals per building, farm map is small) |
| Distributed infrastructure | **N/A** — no queues, caches, circuit breakers, or cloud resources |
| Resilience | **Applicable** — building-skip, warp-handoff, animal stuck-skip, location-aware cleanup |
| Performance | **Applicable** — lazy-load scan, bounded warps, O(1) re-targeting |
| Maintainability | **Applicable** — seam wrapping, pure Core types |

---

## PAT-U16-01 — Skip-and-Continue (Resilience)

**What**: When a building cannot be entered or its interior fails to load, the batch for that building is skipped entirely. The shift continues with the next batch. No collected items are lost; the orchestrator keeps its buffer.

**Applies to**: REL-U16-01 (building-nav failure is a handled, local outcome), SAFE-U16-04 (tolerate absent data).

**How**:
- `BuildingWorkNavigator.TryEnter(building)` returns a `bool` (or a discriminated outcome) rather than throwing.
- On failure, `ShiftOrchestrator` logs `log.building.skipped` at `LogLevel.Warn` (NFR-DES-Q2=A) and calls `BuildingWorkNavigator.Skip(batch)` — which takes no warp action and signals the orchestrator to advance the batch index.
- The `ShiftContext` work buffer is untouched (any previously collected items from earlier batches remain safe).
- This mirrors the existing outdoor-tile skip-and-continue pattern (tile unreachable → skip task, continue to next).

**Scope**: Building entry only. Interior-task failures (e.g., an individual animal task) use PAT-U16-02.

---

## PAT-U16-02 — Bounded Give-Up (Resilience)

**What**: An unreachable target (animal or tile) causes a skip after the stuck window expires — no unbounded chasing, no new give-up machinery.

**Applies to**: REL-U16-02 (moving/unreachable animals bounded by `StuckDetector`), REL-U16-03 (scan fixes identity, execution re-validates live), NFR-Q3=A.

**How**:
- The worker targets an animal's **live** position (re-resolved on the throttled work tick, O(1) position read — PAT-U16-04).
- The existing U-13 `StuckDetector` runs on the same cadence as outdoor navigation. If it fires, `AnimalTaskHandler` records the animal as skipped and signals the orchestrator to move on.
- The skip is **local to the animal**. Other animals in the same batch continue.
- No separate animal-specific retry counter; StuckDetector is the single bounded give-up mechanism.

---

## PAT-U16-03 — Lazy-Load Interior Scan (Performance)

**What**: Each building interior is scanned at the moment the worker enters that batch — not pre-scanned at 6am and not re-scanned during the batch. One scan per location per shift.

**Applies to**: PERF-U16-01 (lazy scan once per location), NFR-Q2=A.

**How**:
- `IndoorWorkScanner.Scan(GameLocation interior)` is called once, immediately after the enter-warp succeeds.
- The returned `WorkBatch` is held by the orchestrator for the duration of the batch (NFR-DES-Q3=A — stateless scanner).
- If the enter-warp fails (PAT-U16-01), the scan is never called — avoiding any work on a location the worker cannot reach.
- The `(0,0)..(999,999)` zone placeholder is resolved to the interior's real map bounds inside `IndoorWorkScanner` before the `DetectTask` call (TS-U16-06).

---

## PAT-U16-04 — Identity-Stable Scan / Live Execution (Correctness)

**What**: The scan locks in *which* animals to care for (by stable `AnimalRef.Id`); the execution phase re-validates *whether* care is still needed and re-resolves *where* the animal is — every approach tick.

**Applies to**: REL-U16-03, REL-U16-02, NFR-Q2=A/NFR-Q3=A interaction.

**How**:
- `IndoorWorkScanner` (and the outdoor-farm batch builder) produces `AnimalWorkItem` records with only `AnimalRef` (stable id + home location + display name) — not a frozen tile position.
- At execution time, `AnimalTaskHandler` resolves the animal's **current** tile from the live `FarmAnimal` object before each approach. It re-resolves on each movement tick while approaching (O(1) position read, within the existing 4-tick throttle).
- Eligibility ("still needs petting", "still has a product") is also re-validated live — so an animal cared for between scan and arrival is cleanly skipped (not double-handled).
- Tile work (crops, rocks, trees) still freezes tile positions at scan time — that pattern is unchanged.

---

## PAT-U16-05 — Seam Wrapping (Maintainability)

**What**: All Stardew Valley / SMAPI API access for buildings, warps, and animals is confined to the three new Mod-layer helpers. The Core layer gains only pure data types.

**Applies to**: MAINT-U16-01 (new orchestration confined to Mod layer), NFR-MAINT-03.

**How**:
- `BuildingWorkNavigator` owns all building/warp API access (`Building.humanDoor`, location `characters` collection, `currentLocation`, entry `Position`).
- `IndoorWorkScanner` owns the interior `GameLocation` scan.
- `AnimalTaskHandler` owns all `FarmAnimal` API access (`pet`, `currentProduce`, `GetHarvestType`, hopper/hay APIs).
- `Dayswork.Core` receives only: `WorkBatch`, `BatchKind`, `AnimalWorkItem`, `AnimalRef`, `AnimalProductKind`, and `LocationName` on `WorkItem` — all pure value types.
- Exact Stardew member names (confirmed at Code Generation per TS-U16-03/04) stay inside the seam implementations.

---

## PAT-U16-06 — Location-Aware Cleanup (Safety)

**What**: The worker is removed from **whatever location it currently occupies** on shift end, `ClearWorker`, or sleep-stop — not from a hardcoded `Game1.getFarm()`.

**Applies to**: SAFE-U16-02 (worker never serialized into any location), NFR-SAFE-03.

**How**:
- `ShiftOrchestrator.ClearWorker()` (and the sleep-stop path `StopForSleepAndSettle()`) reads `worker.currentLocation` and removes the NPC from that location's `characters` list.
- This single change covers: worker on the farm (normal case), worker inside a barn or coop (mid-batch when cap fires or sleep occurs), worker in the Greenhouse.
- The warp handoff (PAT-U16-01) always sets `currentLocation` before removing from the old location, so `currentLocation` is always accurate at cleanup time.

---

## PAT-U16-07 — Bounded Warp Budget (Performance)

**What**: Warps are a one-time handoff per building entry/exit during work, and per building-interior chest during the deposit run — never per-frame.

**Applies to**: PERF-U16-03 (warps bounded, not per-frame), REL-U16-04 (warp handoff robust).

**How**:
- Work phase: at most 1 enter + 1 exit per selected building (enter before tile/animal work; exit after).
- Deposit run: at most 1 enter + 1 exit per building-interior chest (TS-U16-10). Farm-side chests and the shipping bin run without a warp.
- A warp is a synchronous handoff (remove from old `characters`, add to new, set `Position`/`currentLocation`) — no SMAPI event needed, no frame-by-frame cost.
- If the warp handoff cannot complete (REL-U16-04), it is treated as a building-nav failure (PAT-U16-01): skip the building, log at Warn.

---

## PBT Compliance Summary

| PBT Requirement | Pattern covered | Approach |
|---|---|---|
| PBT-U16-01 — Shift-plan partitioning & ordering | PAT-U16-03 (lazy scan determines batch), PAT-U16-04 | Property + unit: `BuildShiftPlan` maps zones to batches, orders AnimalBuilding→Interior→OutdoorFarm |
| PBT-U16-02 — Animal-task ordering within batch | PAT-U16-04 | Reuses `TaskPriorityOrderer` stable-sort PBT |
| PBT-U16-03 — Deposit conservation with animal products | PAT-U16-07 | Reuses U-14 `DepositPlanner` properties |
| PBT-U16-04 — Refund formula unchanged | (no new billing pattern) | Reuses U-05 `RefundCalculator` invariants |
| PBT-U16-05 — State-machine invariants unchanged | MAINT-U16-02 (no new SM phase) | Reuses U-10 `ShiftStateMachine` properties |
| PBT-U16-06 — Seed logging | All new properties | Follows U-02 seed + shrunk-input convention |

**Play-tested (not PBT)**: door-warp handoff, feed-bench placement, milk/shear/egg/truffle collection, live animal targeting + stuck-skip, building-skip, full vanilla animal-care gains — all read live game/SMAPI state.
