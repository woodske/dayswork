# U-16 — Animals & Buildings: NFR Design Plan

**Unit**: U-16 — Animals & Buildings
**Phase**: CONSTRUCTION — NFR Design
**Builds on**: approved NFR Requirements (NFR-Q1=A…Q3=A; SAFE/PERF/REL/UX/MAINT/COMPAT/PBT-U16-01..06). See [nfr-requirements/](../u-16-animals-buildings/nfr-requirements/).

---

## Plan Checklist

- [x] NFR-DES-Q1–Q3: Collect answers (Q1=A constructor-injected, Q2=A LogLevel.Warn, Q3=A stateless scanner)
- [x] Resolve any ambiguities — none; all recommended options chosen
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-16's NFR design is **lightweight** relative to typical service-oriented units. This is a single-player Stardew Valley SMAPI mod with no network surface, no cloud components, and no horizontal scaling. Most "standard" NFR design categories are explicitly N/A:

- **Security** — Disabled project-wide (NFR-SEC-01 / Extension Configuration)
- **Scalability** — Bounded by game design (farm ≤ ~500 effective tiles, ≤ 12 animals per building)
- **Availability / HA** — Single-player local process; no uptime SLA
- **Distributed infrastructure** — No queues, caches, circuit breakers, or cloud resources

The design-relevant NFR concerns for U-16 are:

1. **Resilience patterns** — building-skip-and-continue (REL-U16-01), warp-handoff robustness (REL-U16-04), stuck-skip for animals (REL-U16-02/03), location-aware ClearWorker (SAFE-U16-02)
2. **Performance patterns** — bounded once-per-location scan, bounded warps, O(1) re-targeting (PERF-U16-01..04 — all specified; no new design decisions needed)
3. **Logical components** — three new Mod helpers (`BuildingWorkNavigator`, `IndoorWorkScanner`, `AnimalTaskHandler`) and how they integrate with the existing `ShiftOrchestrator` seam architecture (TS-U16-02)

The questions below focus on the three areas where a design choice meaningfully shapes the `nfr-design-patterns.md` and `logical-components.md` artifacts.

> Option **A** is the recommendation. A letter is enough; add a sentence to steer detail.

---

## NFR Design Questions

### NFR-DES-Q1 — New helper injection pattern (logical-component boundary)

`BuildingWorkNavigator`, `IndoorWorkScanner`, and `AnimalTaskHandler` are new Mod-layer components (TS-U16-02). They must be available to `ShiftOrchestrator`. How should they be wired in?

**A) Constructor-injected seams (Recommended).** All three are passed into `ShiftOrchestrator` (or a companion `ShiftOrchestratorBuildingExtension` helper) via constructor — consistent with how `ChestResolver`, `StuckDetector`, and `MailDispatcher` are wired today. Testable at the seam; the orchestrator remains unaware of Stardew/SMAPI internals. `ModEntry` wires them at startup just as it wires the other seams.

**B) Instantiated inline inside ShiftOrchestrator.** The orchestrator news up the helpers directly, accepting the tighter coupling in exchange for simpler wiring. No injection needed; harder to stub in tests.

**C) Other (describe after the tag).**

[Answer]: A

---

### NFR-DES-Q2 — Log severity for building-skip events (REL-U16-01 / UX-U16-02)

When a building is skipped — demolished, door unreachable, interior fails to load — the string `log.building.skipped` is emitted (UX-U16-02). What SMAPI log level should `BuildingWorkNavigator` use?

**A) `LogLevel.Warn` (Recommended).** Consistent with other handled-but-notable outcomes in the mod (e.g., missing silo → `log.animal.no_silo` at Warn, stuck escalation → Warn). Visible in the SMAPI console by default; signals "something unexpected happened but I recovered."

**B) `LogLevel.Debug`.** Only visible when the player enables verbose logging. Lower noise during normal play; may make diagnosis harder.

**C) `LogLevel.Error`.** Maximally visible but semantically wrong: a demolished building is a player-controlled state change, not a mod error. Risks alarming players unnecessarily.

[Answer]: A

---

### NFR-DES-Q3 — IndoorWorkScanner result ownership (stateless vs. stateful component)

`IndoorWorkScanner` scans a building interior once at batch entry (PERF-U16-01 / NFR-Q2=A). After the scan, who holds the result?

**A) Caller owns the result — scanner is stateless (Recommended).** `IndoorWorkScanner.Scan(location)` returns the `WorkBatch` and forgets it. The orchestrator holds the result in the batch it is currently executing, exactly as the outdoor farm scan result is held today in `ShiftContext.WorkItems`. Pure, no lifecycle to manage, consistent with how `DetectTask` works today.

**B) Scanner caches internally, keyed by location name.** `IndoorWorkScanner` retains a `Dictionary<string, WorkBatch>` for the shift's duration; callers ask it for the cached result later. More autonomous; adds mutable state to a helper that would otherwise be purely functional.

[Answer]: A

---

## Artifact output (generated after answers are collected)

- `aidlc-docs/construction/u-16-animals-buildings/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-16-animals-buildings/nfr-design/logical-components.md`
