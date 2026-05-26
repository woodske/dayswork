# U-21 — NFR Design Patterns

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply, along with functional-design decisions FD-Q1=A through FD-Q9=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-21 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local single-worker in-process runtime seam; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no service deployment, queue, cache server, or async worker runtime |
| Resilience | **Applicable** — unified stop-and-settle behavior, output safety, and preserved stuck/sleep interruption handling |
| Performance | **Applicable** — lightweight per-tick loop, synchronous per-beat stamina updates, explicit pacing knobs |
| Determinism / correctness | **Applicable** — strict deterministic pure-runtime seams are a hard quality bar |
| Maintainability / testability | **Applicable** — pure Core ownership of stamina/boundary rules plus strong example/property coverage |

---

## PAT-U21-01 — Per-Beat Synchronous Energy Ledger

**What**: Actual labor beats update stamina immediately through a single pure ledger path, and the visible overhead bar reflects that result in the same logical beat.

**Applies to**:
- `PERF-U21-03` per-beat stamina updates remain cheap
- `USAB-U21-01` overhead stamina feedback is immediate and deterministic
- `TS-U21-02` keep stamina logic in pure Core seams
- `TS-U21-04` keep per-beat updates synchronous and immediate

**How**:
- live labor action occurs
- orchestrator maps it to `WorkActionKind`
- `WorkerEnergyLedger.ApplyActionCost(...)` updates `WorkerEnergyState`
- orchestrator pushes the new state to the overhead energy-bar presentation immediately
- no delayed event queue, batching layer, or async HUD sync is introduced

**Why this pattern**:
- keeps the worker's visible effort tightly coupled to its actual labor
- preserves deterministic behavior for tests
- avoids hidden timing drift between runtime truth and NPC presentation

**Not responsible for**:
- deciding whether the current work unit is complete
- deciding when the shift should enter wrap-up

Those belong to the next pattern.

---

## PAT-U21-02 — Work-Unit Boundary Stop Gate

**What**: Zero stamina and 8pm do not stop the worker mid-action; they become stop reasons only at explicit work-unit boundaries.

**Applies to**:
- `REL-U21-03` stop reasons converge into predictable wrap-up behavior
- `REL-U21-04` zero-stamina behavior remains legible and stable
- `PBT-U21-02` boundary-stop invariants
- `TS-U21-02` keep work-boundary logic in pure Core seams

**How**:
- the current work unit remains in progress until it resolves
- zero stamina may be reached during the unit, but the unit is allowed to finish
- once the unit resolves, the pure stop-decision seam decides whether a new unit may begin
- if not, the shift transitions into wrap-up

**Why this pattern**:
- matches the approved player-facing rule set
- prevents awkward mid-swing / mid-interaction aborts
- gives the runtime a crisp decision boundary that is easy to reason about and test

---

## PAT-U21-03 — Unified Stop-and-Settle Path

**What**: Different stop reasons converge into one shared output-safe wrap-up path instead of branching into separate settlement systems.

**Applies to**:
- `REL-U21-03` stop reasons converge into predictable wrap-up behavior
- `SAFE-U21-01` no collected items are lost on any stop path
- `SAFE-U21-02` no hidden refund/debt settlement
- `SAFE-U21-03` interrupt paths preserve item safety and consistent runtime behavior
- `TS-U21-05` reuse the existing output/deposit pipeline
- `TS-U21-06` remove refund/debt concerns from active runtime seams

**How**:
- normal completion, zero stamina, 8pm, sleep-stop, and stuck abort all enter the same conceptual wrap-up path
- the existing item buffer, deposit planner, and overflow rules remain the output-safety mechanisms
- deposit behavior stays part of the same wrap-up model even when stamina is already zero
- no separate refund/debt calculation branch is allowed in the runtime cutover path

**Why this pattern**:
- lowers edge-case divergence between stop reasons
- preserves the strongest existing safety invariant with minimal new machinery
- keeps the redesign story clean: work budget and billing are separate concerns

---

## PAT-U21-04 — Explicit Config-Driven Pacing Profile

**What**: Slower readable worker behavior is represented by explicit pacing inputs rather than by ad hoc delays or expensive runtime drag.

**Applies to**:
- `PERF-U21-02` slower feel comes from explicit timing
- `USAB-U21-02` slower pacing improves readability without feeling broken
- `USAB-U21-04` arrival and wrap-up remain easy to read
- `TS-U21-03` keep pacing explicit and config-driven

**How**:
- define a dedicated pacing profile or equivalent runtime seam
- centralize movement slowdown, entrance hold, and action-cadence values
- apply those values intentionally in movement/action orchestration
- keep the pacing layer deterministic and lightweight

**Why this pattern**:
- makes pacing tunable without another architecture pass
- prevents "slow because inefficient" behavior
- supports later GMCM/config exposure naturally

---

## PAT-U21-05 — Thin Orchestrator, Stateful Core Decisions

**What**: `ShiftOrchestrator` remains the live world adapter, while the stateful decision logic for stamina and stop boundaries stays in pure Core seams.

**Applies to**:
- `MAINT-U21-01` new stamina/work-boundary logic stays in pure Core seams
- `COMPAT-U21-01` runtime bridge behavior may remain temporarily, but it must not own the new budget model
- `TS-U21-01` stay on the existing SMAPI event-driven runtime shell
- `TS-U21-02` keep stamina and boundary logic in pure Core seams

**How**:
- orchestrator performs world actions and translates SMAPI/Stardew signals
- pure Core seams compute energy results, work-boundary decisions, and stop outcomes
- orchestrator reacts to those results rather than inventing the rules locally

**Why this pattern**:
- preserves testability for the hardest new logic
- keeps the runtime retrofit incremental
- avoids turning `ShiftOrchestrator` into an opaque pile of stateful branches

---

## PAT-U21-06 — Dedicated Stateful Regression Support

**What**: U-21’s stronger regression bar is satisfied through explicit example tests plus property-oriented sequence coverage for the new stateful runtime decisions.

**Applies to**:
- `MAINT-U21-02` strong example + property coverage
- `MAINT-U21-03` property coverage targets runtime invariants
- `MAINT-U21-04` stateful testing is strongly recommended
- `PBT-U21-01` energy invariants
- `PBT-U21-03` deterministic pure-runtime outcome invariants
- `PBT-U21-04` no-refund runtime invariants
- `TS-U21-08` tests stay on `xUnit` + `FsCheck`

**How**:
- example tests pin concrete runtime stories such as "zero on final tree swing" and "sleep-stop still settles output"
- property tests cover invariants over generated beat sequences
- optional model/sequence helpers stay on the test side only
- full UI or world automation remains unnecessary for this unit

**Why this pattern**:
- U-21 introduces stateful logic that is too easy to regress with example tests alone
- property/state-sequence coverage is the best fit for the enabled partial PBT mode
- test-side helpers let us exercise the hard logic without expanding production architecture

---

## Pattern Summary

U-21’s NFR design stays intentionally focused:
- one synchronous per-beat energy-ledger path
- one explicit work-unit boundary gate for stop conditions
- one unified stop-and-settle path
- one config-driven pacing profile
- one thin-orchestrator / stateful-Core separation
- one dedicated stateful regression-support strategy

That gives the runtime retrofit a strong performance, determinism, and safety bar without adding infrastructure or rewriting the worker system around a new framework.
