# U-13B — NFR Requirements

**Unit**: U-13B — Farmer Worker + Tool Visuals

U-13B is a re-platforming unit: it swaps the worker entity (`NPC` → `Farmer`), the movement mechanism (native `PathFindController` → manual driver), and adds manual rendering, randomized appearance, and tool-swap visuals. It changes **no** behavioural logic, so the U-13 behavioural NFRs (priority/skip scan cost, stuck termination, invulnerability debounce, Core purity of `StuckDetector`/state machine) remain in force unchanged and are not restated. This unit owns the Farmer-specific NFRs that U-13 deferred at the split.

---

## Performance

### PERF-U13B-01 — Per-frame worker draw is bounded *(was deferred from U-13 as PERF-U13-03)*
The worker `Farmer` is drawn on **every rendered frame** (drawing must be smooth, so it is not throttled). Cost is a single `FarmerRenderer` composite draw — the same machinery the game already runs for the player and for remote multiplayer farmhands — plus a shadow and an occasional emote bubble. Well under the per-frame budget. *Satisfies NFR-PERF-01.*

### PERF-U13B-02 — Movement path computed per-target, not per-tick *(was deferred from U-13 as PERF-U13-04)*
A tile path is computed once per navigation target (on shift start, work-list advance, or teleport recovery) by reading a throwaway `PathFindController.pathToEndPoint`; the controller is discarded immediately. Following the path is O(1) `Position` steps per sampled tick. No per-tick path search. *Satisfies NFR-PERF-01/02.*

### PERF-U13B-03 — Retain the UpdateTicked throttle for work logic
The work/stuck/hit logic continues to run only every **4 ticks** (~15 Hz) via the existing modulo early-return. The renderer draws every frame independently of this throttle. *Satisfies NFR-PERF-01.*

### PERF-U13B-04 — Appearance generated once per shift
`WorkerAppearanceRandomizer.Generate(contractId)` runs once at 6am when the worker is created; the result is applied to the `Farmer` and reused for the whole shift. No per-frame or per-tick appearance work. *Satisfies NFR-PERF-01.*

---

## Safety & Data Integrity

### SAFE-U13B-01 — Farmer never serialized (NFR-SAFE-03)
The worker `Farmer` is created at 6am, removed at shift end, and removed on `OnSaving` (with the existing refund handling) if a shift is active at save time. It is **never** added to `location.characters`, `location.farmers`, or any other game-managed/serialized collection — held only by the mod's own reference, and (per FD-Q2=A) drawn via a SMAPI render event rather than any game draw collection. Guards against save corruption. *(Carried from U-13 SAFE-U13-03, which already applied to both units.)*

### SAFE-U13B-02 — Deposit / refund / debris guarantees preserved (NFR-SAFE-01/02/04)
U-13B does not touch the deposit, refund, or `CollectNewDebris` logic. NFR-SAFE-01 (no items lost on early-end), NFR-SAFE-02 (integer-clamped refund), and NFR-SAFE-04 (worker only buffers self-caused drops) are preserved unchanged by BR-PRESERVE-01. The only seam change is teleport recovery setting `Farmer.Position` directly instead of `Game1.warpCharacter`, which has identical end-state semantics (worker relocated, no item/gold side effects). *Satisfies NFR-SAFE-01/02/04 by preservation.*

---

## Reliability

### REL-U13B-01 — Skip-and-continue on no-path (retained)
A navigation whose A* yields no path raises `NavigationFailed`, and the orchestrator advances to the next work item without error — identical to the U-13 adapter contract. *(FR-WORK-08.)*

### REL-U13B-02 — Renderer and movement driver are null-safe
`WorkerRenderer.OnRenderedWorld` is a no-op when no worker is active or the rendered location is not the worker's location. The movement driver tolerates an empty / zero-length path (immediate `HasArrived`). Neither throws during the between-shifts idle state.

### REL-U13B-03 — Appearance never produces invalid sprite indices
All randomized appearance indices are clamped to valid character-creation ranges, so no out-of-range hair/shirt/accessory index can be applied to the `Farmer` (which would otherwise risk a draw-time crash). *(NFR-SAFE-03 spirit — robustness.)*

### REL-U13B-04 — Behaviour-preservation regression guard
Per BR-PRESERVE-01, the existing U-13 worker behaviour (priority queue, capability/skip, stuck escalation, deposit/exit, save handling) must remain green. The U-13 Core test suite (173 passing) must continue to pass after the U-13B changes; the entity/movement/draw seams are exercised by play-test. The U-13 "worker stands still" symptom must be resolved by the manual per-tick stepping (BR-WORKER-02) — reliable arrival is a Definition-of-Done item.

---

## Maintainability

### MAINT-U13B-01 — Core purity (NFR-MAINT-03)
The one new Core type, `WorkerTool` (enum + `ForTask` map), lives in `Dayswork.Core` with **zero** Stardew/SMAPI references. Verified by the Core csproj reference list.

### MAINT-U13B-02 — Stardew refs confined to the Mod layer (NFR-MAINT-03)
All game-coupled additions — `FarmhandWorker` (Farmer), `WorkerMovementDriver`, `WorkerRenderer`, `WorkerAppearance(+Randomizer)`, `ToolSwapAnimator` (the frame-set/`FarmerSprite` parts) — live in `Dayswork` and hold the Stardew/SMAPI references.

### MAINT-U13B-03 — No new Harmony patches (NFR-MAINT-04)
Per FD-Q2=A, rendering is done via the `Display.RenderedWorld` SMAPI event, not a draw-pass Harmony patch. U-13B introduces no new Harmony patches. *(NFR-MAINT-04 satisfied; true world-occlusion via a draw patch is explicitly deferred.)*

### MAINT-U13B-04 — No new user-visible strings (NFR-UX-02)
U-13B adds no new `i18n/default.json` keys: the worker has no name plate/portrait dialogue in this unit (the `npc.farmhand.name` key existed for the NPC and is retained or retired during code-gen without new text), and the "?"/"!" reactions are vanilla emotes. *(NFR-UX-02 N/A for new strings this unit.)*

### MAINT-U13B-05 — .NET conventions (NFR-MAINT-05)
Code follows standard .NET conventions (`dotnet format`).

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

The only pure logic added this unit is `WorkerTool.ForTask`, a **finite, total** mapping from `TaskKind` to `WorkerTool`. It is exhaustively verifiable by a table-driven test (every `TaskKind` → expected tool), which is stronger and clearer than a generated property for a finite domain. Therefore:

- **PBT-03**: no new *property* obligation this unit — the finite map is covered by an exhaustive `[Theory]`/table test instead. (PBT-03's intent — invariants on pure logic — is met.)
- **PBT-08**: if any property is nonetheless added, it follows the U-02 seed + shrunk-input logging convention.
- **PBT-02 / PBT-07 / PBT-09**: **N/A** — no new round-trip serialization type, no new shared-generator obligation, framework already chosen.

All movement/render/appearance/tool-swing code reads or drives live Stardew state and is **play-tested**, not property-tested.

---

## Extension compliance summary

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled project-wide (no network/PII/auth surface); U-13B adds none. |
| Property-Based Testing (Partial) | Compliant | Only new pure logic (`WorkerTool` map) is finite/total → exhaustive table test satisfies PBT-03 intent; PBT-08 honored if a property is added; PBT-02/07/09 N/A. U-13 Core properties remain green (REL-U13B-04). |
