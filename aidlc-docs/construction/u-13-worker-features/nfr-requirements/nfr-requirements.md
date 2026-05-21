# U-13 — NFR Requirements

> **SCOPE SPLIT (2026-05-21):** Farmer-specific NFRs — **PERF-U13-03** (per-frame Farmer draw) and **PERF-U13-04** (manual movement stepping) — are **deferred to U-13B**. SAFE-U13-03 (never-serialize) applies to both (U-13's NPC is already removed on save). All other U-13 NFRs (priority/skip scan, stuck, invulnerability, Core purity, PBT) stay in U-13.

**Unit**: U-13 — Worker AI: Priority + Capability/Skip + Stuck + Invulnerability *(Farmer NFRs → U-13B)*

Inherits U-10's worker NFRs; adds requirements for the full-Farmer rendering/movement, stuck escalation, and capability integration.

---

## Performance

### PERF-U13-01 — Retain the UpdateTicked throttle
The work/stuck/hit logic continues to run only every **4 ticks** (~15 Hz). Skipped ticks early-return on a modulo check. *Satisfies NFR-PERF-01.*

### PERF-U13-02 — Work list (with capability checks) built once per shift
The scan now also classifies each object into an `AxeTarget`/`PickTarget` and applies `CapabilityEvaluator` skip rules, plus trellis-neighbor resolution. This remains a **one-time** cost at shift start: O(n) scan + O(1) capability lookups + O(n log n) per-group nearest-first sort. Worst case (~520 candidate tiles on Standard Farm) is microseconds. *Satisfies NFR-PERF-02.*

### PERF-U13-03 — Per-frame worker draw is bounded
The worker `Farmer` is drawn on **every rendered frame** (drawing must be smooth, so it is not throttled). Cost is a single `FarmerRenderer` draw — the same machinery the game already runs for the player and remote farmhands. Well under the per-frame budget. *Satisfies NFR-PERF-01.*

### PERF-U13-04 — Movement path computed per-target, not per-tick
A tile path is computed once when a new navigation target is set (on arrival/advance/teleport), then followed by O(1) position steps on each sampled tick. No per-tick path search. *Satisfies NFR-PERF-01/02.*

### PERF-U13-05 — Stuck + hit detection are O(1) per sampled tick
`StuckDetector.RecordTick`/`ShouldFireStuck` and the melee-swing proximity check are constant-time. Negligible. *Satisfies NFR-PERF-01.*

---

## Safety & Data Integrity

### SAFE-U13-01 — No items lost on stuck early-end (NFR-SAFE-01)
The step-3 early end runs the normal Depositing → Exiting path: all buffered items go to the shipping bin (infinite capacity, FR-OUT-06) before the worker exits. Identical guarantee to the 8pm cap. (Multi-chest + mail-on-unreachable is U-14.)

### SAFE-U13-02 — Integer-clamped refund on early-end (NFR-SAFE-02)
The early end captures an end time and computes `refund = clamp(deposit − hoursWorked × rate, 0, deposit)` with integer arithmetic — same as the 8pm path. No floating-point gold leakage.

### SAFE-U13-03 — Farmer never serialized (NFR-SAFE-03)
The worker `Farmer` is created at 6am, removed at shift end, and removed on `OnSaving` (with full deposit refund) if a shift is active at save time. It is **never** added to `location.characters`, `location.farmers`, or any other game-managed/serialized collection — held only by our own reference. Guards against save corruption.

### SAFE-U13-04 — Worker only collects self-caused drops (NFR-SAFE-04)
The U-10 `CollectNewDebris` snapshot-diff is retained: only debris created by the worker's own action between before/after snapshots is buffered. Player-dropped/placed items are untouched.

---

## Reliability

### REL-U13-01 — Stuck escalation always terminates cleanly
Escalation is bounded by `RecoveryAttempts`: at most one teleport-and-resume, then a guaranteed end-shift. No infinite recovery loop is possible.

### REL-U13-02 — Teleport target must be reachable
Step-2 teleport selects the next **reachable** task tile; if none is reachable (or stuck fires again), escalation goes straight to end-shift rather than stranding the worker.

### REL-U13-03 — Skip-and-continue on no-path (retained)
A normal navigation that yields no path advances to the next work item without error (FR-WORK-08).

### REL-U13-04 — Object classifier never throws on unknown types
The Mod-side object→target classifier returns "skip" for any object class it cannot map; only the pure `CapabilityMatrix` throws `ArgumentOutOfRange`, and it is only ever called with known enum values produced by the classifier. Unknown game objects are skipped, not errored.

### REL-U13-05 — Ouch-emote debounce
Hit detection fires at most one emote per player swing and never mutates shift state or interrupts the current intent, so it cannot interfere with the work loop or stuck detection.

---

## Maintainability

### MAINT-U13-01 — Core purity (NFR-MAINT-03)
`StuckDetector` (C-09) and the extended `ShiftStateMachine` (C-08) live in `Dayswork.Core` with **zero** Stardew/SMAPI references. Verified by the Core csproj reference list. These are the PBT targets.

### MAINT-U13-02 — Stardew refs confined to the Mod layer
All game-coupled additions — the Farmer-backed worker, movement driver, `ToolSwapAnimator`, render hook, hit-detector, and object→target classifier — live in `Dayswork` and hold the Stardew/SMAPI references.

### MAINT-U13-03 — No new user-visible strings (NFR-UX-02)
U-13 adds no new `i18n/default.json` keys: the "?" and "!" reactions are vanilla emotes, and the tool-missing warning **text** is owned by U-14's mail. *(NFR-UX-02 N/A for new strings this unit.)*

### MAINT-U13-04 — No new Harmony patches
U-13 introduces no Harmony patches. *(NFR-MAINT-04 N/A.)*

### MAINT-U13-05 — .NET conventions (NFR-MAINT-05)
Code follows standard .NET conventions (`dotnet format`).

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

PBT-03 is **blocking** for U-13's pure Core logic; PBT-08 (seed logging) is blocking. PBT-02 and PBT-07 are **N/A** for U-13 (no new round-trip serialization type and no new shared-generator obligation beyond U-10's `ItemBufferGen`).

### PBT-U13-01 — State machine: terminal invariant (PBT-03 blocking)
For all reachable states, once `Phase == Done`, every `Transition()` throws.

### PBT-U13-02 — State machine: only legal transitions succeed (PBT-03 blocking)
For any phase `s` and any phase `t` not in `s`'s successor set (including the new Stuck/Recovering edges), `Transition(t)` throws; every listed successor succeeds.

### PBT-U13-03 — State machine: Stuck/Recovering reachability (PBT-03 blocking)
`Stuck` is only reachable from `Working`; `Recovering` only from `Stuck`; neither is reachable from `Done`.

### PBT-U13-04 — StuckDetector: progress resets (PBT-03 blocking)
For any tick sequence, a tick with `madeProgress == true` resets the accumulator, so `ShouldFireStuck()` is false immediately after any progress tick.

### PBT-U13-05 — StuckDetector: threshold monotonicity (PBT-03 blocking)
With only no-progress ticks, `ShouldFireStuck()` is false while cumulative elapsed minutes `< threshold` and true once `>= threshold`.

### PBT-U13-06 — StuckDetector: Reset (PBT-03 blocking)
`Reset()` always returns the detector to the not-stuck state regardless of prior accumulation.

### PBT-U13-07 — Seed logging (PBT-08 blocking)
All new U-13 properties follow the U-02 seed + shrunk-input logging convention.

**Not PBT (unit/table-tested instead):** the object→target classifier (reads game objects — Mod layer) and the `BuildWorkList` ordering (reads game state) are covered by table-driven/unit tests, not properties. The pure `CanChop`/`CanBreak` and `TaskPriorityOrderer` properties already exist from U-07.
