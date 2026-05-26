# U-21 — NFR Requirements

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh

U-21 is a runtime retrofit unit. Its NFR surface is centered on **lightweight per-tick execution**, **immediate deterministic stamina/pacing feedback**, **consistent stop-and-settle safety across every shift-end reason**, **strict determinism for the new pure runtime seams**, and **strong example + property-based regression coverage for the new stateful Core logic**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q9=A apply throughout.

---

## Performance

### PERF-U21-01 — The live shift loop remains comfortably lightweight (NFR-Q1=A)
The worker's runtime update loop must stay well below visible frame-drop territory on typical hardware. The added stamina model, overhead bar, and slower pacing must not create noticeable hitching in normal single-worker play.

This unit is not permitted to depend on:
- expensive per-frame recomputation of work planning
- ad hoc polling loops that scale with the whole farm every tick
- heavy UI redraw logic outside the existing world/NPC draw path

### PERF-U21-02 — Slower worker feel comes from explicit timing, not runtime drag
Movement slowdown and readable labor cadence must come from intentional pacing knobs and scheduled action beats, not from inefficient runtime logic or frame-rate-dependent stalls.

### PERF-U21-03 — Per-beat stamina updates remain cheap
Updating `WorkerEnergyState` and the overhead stamina display must be lightweight enough to happen on every actual labor beat without requiring batching, delayed application, or deferred UI refresh.

### PERF-U21-04 — Wrap-up remains bounded
Deposit-and-exit behavior may take visible in-world time, but the runtime implementation must stay operationally simple and bounded:
- reuse the existing output/deposit pipeline
- avoid introducing expensive end-of-shift settlement scans
- avoid duplicate work when stamina is already exhausted

---

## Reliability & Correctness

### REL-U21-01 — Pure runtime decisions are strictly deterministic (NFR-Q4=A)
Equivalent inputs to the new pure runtime seams must produce the same:
- remaining-energy results
- `CanStartNewWorkUnit` decisions
- stop reasons
- work-boundary transition outcomes

across runs and machines.

### REL-U21-02 — Determinism must not depend on incidental world/update history
Pure energy and stop-decision behavior must not vary because of incidental collection ordering, dictionary ordering, or hidden mutable state outside the explicit runtime inputs.

### REL-U21-03 — Stop reasons converge into predictable wrap-up behavior (NFR-Q3=A)
The following shift-end causes must preserve the same output-safety model and broadly the same wrap-up semantics:
- normal work completion
- zero stamina at a work-unit boundary
- 8pm at a work-unit boundary
- player sleep-stop
- stuck-abort termination

### REL-U21-04 — Zero-stamina behavior remains legible and stable
When the worker reaches zero stamina:
- the current work unit may finish
- no new work unit may begin
- deposit-and-exit begins afterward

This behavior must be consistent rather than dependent on the exact object type or beat timing.

---

## Safety & Data Integrity

### SAFE-U21-01 — No collected items are lost on any stop path
U-21 must preserve the project's no-item-loss guarantee across all shift-end reasons. Buffered output must still end up in:
- assigned chest/bin destinations when deliverable
- next-morning overflow mail when needed
- existing special-case fallbacks such as hay behavior where already defined

### SAFE-U21-02 — Runtime must not reintroduce hidden refund/debt settlement
The runtime redesign must not accidentally keep or recreate hidden billing behavior inside shift-stop logic. The player has already paid the explicit contract price; shift-end runtime should not compute new debt/refund state.

### SAFE-U21-03 — Player-visible safety must survive interrupt paths
Sleep-stop, stuck-abort, and zero-stamina stop paths must preserve:
- item safety
- contract/runtime consistency
- predictable worker removal/exit behavior

even when work ends earlier than a normal fully completed shift.

---

## Usability & Interaction Quality

### USAB-U21-01 — Overhead stamina feedback is immediate and deterministic (NFR-Q2=A)
The worker's overhead stamina bar should update on the same logical labor beat that spends stamina. It should feel tightly coupled to visible work, not loosely synced or delayed.

### USAB-U21-02 — Slower pacing improves readability without feeling broken
The worker should visibly read as in-world labor rather than instant automation, but the pacing must still feel intentional and responsive. The design goal is readable effort, not sluggish or erratic behavior.

### USAB-U21-03 — Zero-stamina transition is understandable in-world
The player should be able to infer what happened by observation:
- the bar reaches zero
- the current unit finishes
- the worker stops taking on new work
- the worker performs deposit-and-exit behavior

No extra UI summary or mail is required to explain stamina exhaustion.

### USAB-U21-04 — Arrival and wrap-up remain easy to read
The worker's entrance, active labor, and deposit-and-exit sequence should remain clear enough that a player watching the farmhand can understand what phase the worker is in.

---

## Maintainability & Testability

### MAINT-U21-01 — New stamina/work-boundary logic stays in pure Core seams
U-21 should keep:
- energy arithmetic
- work-unit boundary rules
- stop-reason transitions

in pure Core components rather than scattering them across `ShiftOrchestrator` and NPC code.

### MAINT-U21-02 — Strong example + property coverage is required (NFR-Q5=A)
Because U-21 introduces new stateful Core logic, it carries a strong regression bar. It requires:
- focused example-based tests for important visible/runtime scenarios
- meaningful FsCheck properties for invariants and deterministic outcomes
- explicit regression coverage for stop-path consistency

### MAINT-U21-03 — Property coverage must target the new runtime invariants
At minimum, FsCheck-friendly coverage for U-21 should exercise:
- stamina never goes negative
- zero stamina forbids new work units
- equivalent beat sequences produce equivalent pure-runtime results
- stop transitions occur only at work-unit boundaries
- wrap-up semantics contain no refund/debt behavior

### MAINT-U21-04 — Stateful testing is strongly recommended where practical
Although the project is in partial PBT enforcement mode, U-21 is a strong candidate for model/state-sequence testing. Command-sequence coverage is recommended for:
- repeated labor beats
- boundary-triggered stop transitions
- repeated stop-path handling over state-machine progression

### MAINT-U21-05 — No new runtime architecture is required for this unit
The quality bar should be achieved by clearer seam ownership and stronger tests, not by introducing:
- a job system
- an async runtime pipeline
- a second HUD/render framework
- a new orchestration subsystem

---

## Compatibility / Retrofit Support

### COMPAT-U21-01 — Runtime bridge behavior may remain temporarily, but it must not own the new budget model
U-21 may still consume some compatibility planning inputs while U-22 finishes scope-driven runtime alignment, but those bridge fields must not remain the source of truth for stamina or stop-decision semantics.

### COMPAT-U21-02 — Existing safety systems remain compatible with the new stamina model
The redesign must preserve compatibility with already-landed runtime subsystems such as:
- item buffering
- deposit planning
- overflow handling
- stuck recovery
- invulnerability / hit reaction

---

## Availability / Security / Infrastructure

### AVAIL-U21-01 — No availability-specific requirements
U-21 is an in-process single-player runtime seam. It has no external uptime, failover, or disaster-recovery surface.

### SEC-U21-01 — Security Baseline is N/A
Security Baseline is disabled project-wide. U-21 has no network, auth, or PII surface, so Security Baseline rules are N/A for this unit.

### INFRA-U21-01 — No infrastructure decisions introduced
U-21 requires no cloud, container, service, or deployment mapping beyond the existing `.NET 6` / SMAPI mod runtime.

---

## Property-Based Testing Obligations

### PBT-U21-01 — Energy invariants
Generated beat sequences should preserve:
- `0 <= RemainingEnergy <= DailyCapacity`
- once zero is reached, `CanStartNewWorkUnit` stays false

### PBT-U21-02 — Boundary-stop invariants
Generated work-unit sequences should show that stop transitions triggered by zero stamina or 8pm happen only at resolved work-unit boundaries, never mid-unit.

### PBT-U21-03 — Deterministic pure-runtime outcome invariants
Equivalent pure-runtime inputs and beat sequences should yield identical pure transition/output results.

### PBT-U21-04 — No-refund runtime invariants
Pure wrap-up and stop-transition outputs should not contain refund/debt settlement semantics once U-21 runtime cutover is in place.
