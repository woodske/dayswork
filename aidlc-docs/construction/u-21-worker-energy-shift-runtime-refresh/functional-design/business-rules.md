# U-21 — Worker Energy + Shift Runtime Refresh: Business Rules

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A

Enforceable rules for the redesign-era stamina-limited shift loop. See [business-logic-model.md](business-logic-model.md) for flows, [domain-entities.md](domain-entities.md) for data shapes, and [frontend-components.md](frontend-components.md) for visible stamina presentation.

---

## No deviations introduced at U-21

U-21 does not change the approved redesign direction. It is the unit that formalizes:
- visible worker stamina
- slower readable pacing
- zero-stamina work-unit completion
- no refund/billing settlement at shift end

It also intentionally preserves the earlier approved sleep-stop behavior shape while removing refund semantics from that path.

---

## Runtime ownership and scope boundaries

**BR-ARCH-01 — Pure Core owns the stamina and stop-decision logic.** `WorkerEnergyLedger`, work-unit-boundary classification, and shift stop-decision rules remain in pure Core seams. *(FD-Q9=A, S-19)*

**BR-ARCH-02 — The orchestrator translates world events; it does not become the source of truth for stamina arithmetic.** `ShiftOrchestrator` may execute live actions and adapt SMAPI/Stardew signals, but the business rules for stamina spending and work-boundary stop decisions live outside the game-engine layer. *(FD-Q9=A)*

**BR-ARCH-03 — U-21 does not complete typed-scope runtime alignment.** Compatibility work-planning inputs may remain in place for this unit; deeper runtime alignment of animal-building and greenhouse scope remains owned by U-22. *(unit boundary)*

**BR-ARCH-04 — Runtime shift state no longer uses hourly settlement as a budget model.** Active shift logic must not depend on `DepositAmount`, `HourlyRate`, or refund computation to decide whether work may continue. *(FD-Q7=A, U-21 DoD)*

---

## Stamina accounting

**BR-ENERGY-01 — Stamina is spent only on actual labor beats.** Movement, pathfinding, deposit travel, and idle waiting do not consume worker stamina. *(FR-WORK-15, FD-Q4=A)*

**BR-ENERGY-02 — Every labor beat spends its configured `WorkActionKind` cost when that beat executes.** No unit-level pre-charge and no end-of-unit lump sum may replace per-beat accounting. *(FR-WORK-16, FD-Q4=A)*

**BR-ENERGY-03 — Stamina clamps at zero.** `WorkerEnergyState.RemainingEnergy` never becomes negative. *(FR-WORK-17, U-21 DoD)*

**BR-ENERGY-04 — Zero stamina forbids new work units.** Once the worker has reached zero, `CanStartNewWorkUnit` becomes false for the rest of that shift. *(FR-WORK-17, U-21 DoD)*

**BR-ENERGY-05 — The shift uses the contract's stored energy profile, not current live config, for that day's labor budget.** One-time and already-started recurring days do not drift mid-shift because config changed later. *(terms snapshot semantics)*

---

## Work-unit boundaries

**BR-UNIT-01 — A work unit is the smallest player-visible resolved labor result.** One watered tile, one harvested tile, one cleared object, one petted animal, or one object stage all count as separate units. *(FD-Q3=A)*

**BR-UNIT-02 — Multi-stage objects resolve stage-by-stage.** `Full tree -> stump` and `stump removed` are separate work units. *(FR-WORK-06, FD-Q3=A)*

**BR-UNIT-03 — Zero stamina reached mid-unit does not abort that unit.** The current unit finishes, then the worker transitions into wrap-up. *(FR-WORK-06, FR-WORK-17, FD-Q3=A, FD-Q4=A)*

**BR-UNIT-04 — 8pm obeys the same boundary rule as zero stamina.** If the hard cap is reached during an in-progress unit, the worker resolves that unit, then begins wrap-up. *(FD-Q3=A)*

**BR-UNIT-05 — No new unit begins at zero stamina or after a boundary-triggered 8pm stop.** *(U-21 DoD, FD-Q3=A)*

---

## Priority and pacing

**BR-PRIORITY-01 — Broad task-family priority remains `animals -> crops -> clearing`.** U-21 preserves the existing high-level order while adding stamina and pacing. *(FR-WORK-03, FD-Q2=A)*

**BR-PRIORITY-02 — Within the active non-animal family, nearest-next routing remains allowed.** U-21 does not revert to a rigid static task-kind order inside that family. *(FD-Q2=A)*

**BR-PACE-01 — Slower pacing is explicit, not accidental.** Movement speed and labor cadence are controlled by dedicated runtime pacing knobs. *(FR-WORK-18, FD-Q5=A)*

**BR-PACE-02 — Pacing must be frame-rate-independent.** The intended slow readable feel cannot depend on lag or unstable per-frame behavior. *(FD-Q5=A, NFR-PERF-01)*

**BR-PACE-03 — Both locomotion and labor cadence are slowed.** U-21 must not satisfy the pacing requirement by slowing only movement or only action timing. *(FD-Q5=A)*

---

## Visible stamina and NPC presentation

**BR-NPC-01 — The worker shows one in-world overhead stamina bar during an active shift.** No separate mirrored HUD is required in U-21. *(FD-Q1=A, FR-NPC-03)*

**BR-NPC-02 — The overhead bar reflects authoritative `WorkerEnergyState` only.** It is a projection, not a second source of truth. *(FD-Q1=A, FD-Q9=A)*

**BR-NPC-03 — The overhead bar stays visible at zero while the worker resolves the final in-progress unit and begins wrap-up.** *(FR-WORK-17, FD-Q1=A)*

**BR-NPC-04 — The stamina bar disappears once the shift fully ends and the worker is gone.** *(FD-Q1=A)*

---

## Wrap-up and shift end

**BR-END-01 — Deposit runs do not consume stamina.** Wrap-up output safety is not constrained by the labor budget. *(FD-Q6=A)*

**BR-END-02 — Once wrap-up starts, the worker completes the normal deposit plan before exiting.** Zero stamina does not downgrade chest/bin delivery into mail-only behavior by default. *(FD-Q6=A, FR-WORK-05, FR-WORK-07)*

**BR-END-03 — Shift-end runtime no longer computes refund or debt settlement.** The player already paid the explicit contract price for that day. *(FR-PAY-01, FR-WORK-07, FD-Q7=A, U-21 DoD)*

**BR-END-04 — The worker exits through the normal farm exit path after deposit handling completes.** *(FR-WORK-07, FD-Q6=A)*

**BR-END-05 — All existing item-safety invariants remain intact under the new stamina model.** Buffered output still reaches chest/bin/mail fallback according to the established output pipeline. *(NFR-SAFE-01)*

---

## Sleep-stop, stuck recovery, and invulnerability

**BR-SLEEP-01 — U-21 preserves the existing sleep-stop operational shape.** Player sleep stops the worker, settles buffered output safely, and leaves remaining world work undone. *(FD-Q8=A, FR-DAY-02)*

**BR-SLEEP-02 — Sleep-stop no longer involves refund semantics.** U-21 removes refund/debt language from that path without changing its broad player-visible behavior. *(FD-Q8=A, FD-Q7=A)*

**BR-STUCK-01 — Stuck detection and escalation remain active under the stamina model.** Confused-emote, teleport recovery, and final stuck abort still apply. *(FR-WORK-11, FR-WORK-12, S-16)*

**BR-STUCK-02 — A stuck-aborted shift still honors output safety and no-refund pricing semantics.** *(FR-WORK-12, FD-Q7=A)*

**BR-HIT-01 — Player attacks do not damage the worker or cancel the shift.** The worker reacts visibly but continues the contract. *(FR-NPC-02, S-17)*

---

## Tool capability preservation

**BR-TOOL-01 — Tool capability is snapshotted once at shift start.** Mid-shift player tool changes do not alter the worker's capabilities for that day. *(S-09)*

**BR-TOOL-02 — Stamina accounting does not override capability gating.** A worker with stamina remaining still skips work the snapshotted capability model says cannot be performed. *(S-09)*

**BR-TOOL-03 — Capability evaluation remains independent from stamina and pacing concerns.** These are parallel runtime concerns, not one combined rule engine. *(S-19)*

---

## Property-based testing obligations

Property-Based Testing is enabled in partial mode. U-21 introduces pure runtime seams whose invariants should be carried into code generation planning and tests.

| Rule | Required property / invariant |
|---|---|
| PBT-03 invariant | `WorkerEnergyState` never goes negative, never exceeds capacity, and never regains `CanStartNewWorkUnit` after hitting zero. |
| PBT-03 invariant | Boundary-stop behavior triggers only when a work unit resolves, not during an unresolved unit. |
| PBT-03 invariant | Shift wrap-up paths include deposit/exit behavior but no billing/refund settlement path. |
| PBT-07 generator quality | Generators must cover mixed action sequences, zero-cost edge rejection, multi-stage object boundaries, and stop reasons across normal / zero-energy / 8pm scenarios. |
| PBT-08 shrinking | Counterexamples should shrink to minimal beat sequences such as one unit with one or two labor beats and the smallest stop condition that violates a boundary rule. |
| PBT-09 framework | FsCheck remains the property-based testing framework for these Core seams. |

Recommended example-based companion tests for later code generation:
- one tree stage that reaches zero on its last axe swing
- one watered tile at exactly one remaining stamina
- one shift that stops at 8pm with buffered items still needing deposit
- one sleep-stop path that settles output without refund computation

Security Baseline is disabled project-wide, so its rules are N/A here.
