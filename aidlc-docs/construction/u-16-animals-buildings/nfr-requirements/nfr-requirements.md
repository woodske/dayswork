# U-16 — NFR Requirements

**Unit**: U-16 — Animals & Buildings

Inherits the worker/output/lifecycle NFRs from U-10/U-13/U-13B/U-14/U-15 and adds requirements for the new surfaces: **cross-location door-warp traversal**, **animal task execution**, and **indoor scanning**. FD decisions (FD-Q1=A…Q9=A) and deviations DEV-U16-01..04 apply throughout, plus NFR decisions NFR-Q1=A (full vanilla animal-care gains), NFR-Q2=A (lazy interior scan at batch entry), NFR-Q3=A (reuse stuck detection for moving/unreachable animals).

---

## Safety & Data Integrity

### SAFE-U16-01 — No items lost across every new path (NFR-SAFE-01)
Conservation must hold on each new branch: a skipped building (BR-NAV-02), the 8pm cap firing while inside a building, the multi-location deposit run, and a sleep-stop that occurs inside a building. Every collected item is deposited (live chest/bin) or mailed; nothing collected is dropped on warp, on `Saving`, or across the day-rollover. *(BR-SAFE-01, BR-DEP-01..03)*

### SAFE-U16-02 — Worker is never serialized into any location (NFR-SAFE-03)
The worker NPC must be removed from **whichever location it currently occupies** (farm *or* a building interior), not just the farm, on shift end / `ClearWorker` / sleep-stop. A save while the worker is inside a barn must not persist a stray worker into that building's save data. The worker is also never added to a building's persisted character collection in a way that survives the shift. *(NFR-SAFE-03, extends BR-WORKER serialization rules from U-13B)*

### SAFE-U16-03 — Only animal-caused products/forage collected (NFR-SAFE-04)
Collect gathers only items identified as animal products/forage (floor eggs, milk/wool from the harvest interaction, ground truffles by forage type) — never arbitrary items the player placed or dropped on a floor or on the ground. *(BR-PROD-06, NFR-SAFE-04)*

### SAFE-U16-04 — Tolerate absent animal/building/silo data (NFR-SAFE-03)
Missing or unexpected state degrades gracefully, never a crash: no silo / empty hopper (feed no-ops, BR-FEED-03), demolished or unreachable building (skip batch, BR-NAV-02), a building with no animals (no animal work), an interior that fails to load (skip). *(BR-SAFE-02)*

### SAFE-U16-05 — No new persisted Dayswork data (NFR-SAFE-03)
Building zones already persist in the contract segment; animal/feed/produce state lives in the live game; the settlement letter reuses U-15's next-morning mechanism. U-16 adds **no** new `SaveDataSerializer` surface. *(SAFE-U15-03 carryover)*

---

## Performance

### PERF-U16-01 — Scan once per location per shift, lazily at entry (NFR-PERF-02, NFR-Q2=A)
Each building interior is scanned **once**, the moment the worker enters that batch; the outdoor farm is scanned once when its batch begins. No per-frame scanning. Lazy-at-entry keeps the scan fresh (state at arrival) and avoids scanning a building that became invalid before the worker reached it. *(BR-IND-01, NFR-Q2=A)*

### PERF-U16-02 — Animal work is bounded by animal count (NFR-PERF-01)
Building/animal enumeration is O(animals in selected buildings), computed at batch entry, never per frame. A Deluxe Barn/Coop population is a small constant. *(BR-ANIM-01..03)*

### PERF-U16-03 — Warps are bounded and not per-frame (NFR-PERF-01)
At most one enter + one exit per selected building during work, plus one enter/exit per building-interior **chest** during the deposit run. A warp is a one-time location handoff, not a per-tick cost. *(BR-LOC-04, BR-DEP-02)*

### PERF-U16-04 — Live animal re-targeting stays cheap (NFR-PERF-01)
Re-resolving a target animal's tile while approaching is an O(1) position read on the throttled work tick (the existing per-4-tick cadence), not a re-scan. *(NFR-Q3=A, PERF carryover from U-13)*

---

## Reliability

### REL-U16-01 — Building-nav failure is a handled, local outcome (FD-Q7=A)
An unreachable/demolished/blocked building skips only that batch, keeps buffered items, logs, and continues. It never aborts the shift and never trips the stuck escalation. *(BR-NAV-02)*

### REL-U16-02 — Moving/unreachable animals bounded by stuck detection (NFR-Q3=A)
The worker targets an animal's **live** position and re-targets as it moves; if it cannot reach the animal within the existing stuck window, it skips that animal and proceeds. No unbounded chasing; no new give-up machinery beyond the U-13 `StuckDetector`. *(BR-ANIM-02, NFR-Q3=A)*

### REL-U16-03 — Scan fixes identity, execution re-validates live (NFR-Q2=A / NFR-Q3=A)
The per-location scan fixes the **set of animals by stable ID**, not their positions or a frozen eligibility snapshot. At execution the worker re-validates "still needs petting / still has product" against live state, so: a wandering animal is pursued not missed; an animal handled in one batch is never double-handled; an animal that left its building before the worker arrived is caught in the later outdoor-farm batch. Tile work, by contrast, correctly freezes tile positions at scan time. *(business-logic-model Flow 4, BR-ANIM-02/03)*

### REL-U16-04 — Warp handoff is robust (NFR-SAFE-03)
Moving the worker between locations (remove from old `characters`, add to new, set entry position + `currentLocation`) must leave no orphaned reference in the prior location and must restore the worker cleanly on exit; a handoff that cannot complete is treated as a building-nav failure (REL-U16-01). *(BR-NAV-01/02, SAFE-U16-02)*

### REL-U16-05 — Mid-building cap returns to the farm before deposit (NFR-SAFE-01)
If the 8pm cap fires while the worker is inside a building, it stops batch work and exits to the farm before the deposit run, so the deposit/exit always originate on the farm. *(BR-NAV-03, FR-WORK-06)*

---

## Usability / Gameplay quality

### UX-U16-01 — Worker animal care grants full vanilla gains (NFR-Q1=A)
Feed/Pet/Collect performed by the worker grant the **same** friendship/mood as the player doing it, so product quality progresses normally and the player can keep animals happy via the hired worker. Implemented by routing through the vanilla animal interactions rather than muting them. *(NFR-Q1=A)*

### UX-U16-02 — New strings localizable (NFR-UX-02, S-20)
New user-visible **log** strings (`log.building.entering`, `log.building.skipped`, `log.animal.fed`, `log.animal.no_silo`) route through `I18nHelper` / `i18n/default.json`. No new mail strings — animal-product overflow and the refund reuse U-15's settlement letter. *(BR-I18N-01)*

### UX-U16-03 — No per-product mailbox spam
Animal products that miss their chest fold into the existing single per-shift settlement letter (no separate animal-product letter). *(BR-SET-01, BR-REF-03 carryover)*

---

## Maintainability & Testability

### MAINT-U16-01 — New orchestration confined to the Mod layer (NFR-MAINT-03)
`BuildingWorkNavigator`, `IndoorWorkScanner`, and `AnimalTaskHandler` live in `Dayswork/Orchestration/` and hold all Stardew animal/building/warp references behind those seams. Core gains only **pure** data types (`WorkBatch`, `BatchKind`, `AnimalWorkItem`, `AnimalRef`, `AnimalProductKind`) and a `LocationName` field on `WorkItem`. *(domain-entities.md)*

### MAINT-U16-02 — No new state-machine phase (NFR-MAINT-03, S-19)
Warps ride inside the existing `Working`/`Depositing` phases; the `ShiftStateMachine` phase set and its PBT-03 invariants are unchanged. *(BR-SAFE-03)*

### MAINT-U16-03 — No new Harmony patches (NFR-MAINT-04)
U-16 rides existing SMAPI events and game APIs; it adds no Harmony patch. *(NFR-MAINT-04 N/A for new patches.)*

### MAINT-U16-04 — .NET conventions (NFR-MAINT-05)
Code follows standard .NET conventions (`dotnet format`).

---

## Compatibility

### COMPAT-U16-01 — No new dependency (NFR-COMPAT-04)
U-16 adds no NuGet package and no manifest dependency. MFM is already required (U-14); GMCM stays optional (U-17). *(COMPAT-U15-01 carryover)*

### COMPAT-U16-02 — All vanilla farm types and building types (NFR-COMPAT-02)
Animal/building handling targets vanilla building types (Coop/Barn families, Greenhouse, Shed, etc.) across all 7 farm types; the Greenhouse's non-standard interior linkage is handled via the existing `ChestResolver.GetBuildingOutlines` by-type fallback. Modded buildings are best-effort. *(FR-COMPAT-02, BR-IND-02)*

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

U-16 is largely SMAPI/Stardew-driven (animal interactions, warps) and primarily play-tested; PBT/unit coverage applies to pure helpers. PBT-08 (seed logging) is blocking for any new property.

### PBT-U16-01 — Shift-plan partitioning & ordering
For any set of zones, `BuildShiftPlan` maps each zone to exactly one batch and orders batches AnimalBuilding → Interior → OutdoorFarm (property + unit). *(BR-LOC-01/02, PBT-03/PBT-07/PBT-08)*

### PBT-U16-02 — Animal-task ordering within a batch
Animal work orders Feed → Pet → Collect — reuses `TaskPriorityOrderer`'s existing stable-sort PBT. *(BR-LOC-03)*

### PBT-U16-03 — Deposit conservation with animal products
`DepositPlanner` conservation and trip-count invariants still hold when animal-product stacks are present — reuses U-14 DepositPlanner properties. *(BR-DEP-01/03)*

### PBT-U16-04 — Refund formula unchanged
Reuses U-05 `RefundCalculator` invariants; warps/deposit-run unbilled. *(BR-DEP-04)*

### PBT-U16-05 — State-machine invariants unchanged
Reuses U-10 `ShiftStateMachine` properties; no new phase. *(BR-SAFE-03)*

### PBT-U16-06 — Seed logging (PBT-08 blocking)
All new U-16 properties follow the U-02 seed + shrunk-input logging convention. *(PBT-08)*

**Not PBT (unit-tested / play-tested instead):** door-warp handoff, feed-bench placement from the hopper, milk/shear/egg/truffle collection, live animal targeting + stuck-skip, building-skip, and full-vanilla animal-care gains read live game/SMAPI state and are integration- or play-tested per the U-16 Definition of Done.

---

## Security
Security Baseline extension is **disabled** project-wide (NFR-SEC-01): no network, PII, auth, or external-input surface. All Security Baseline rules are **N/A** for U-16.
