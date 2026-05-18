# Application Design Plan — Dayswork

**Status**: Awaiting answers to embedded questions. Reply "done" or "approve" when complete, or just answer interactively.

**Scope**: This stage identifies high-level **components**, their **methods (signatures only)**, the **services** that orchestrate them, and **dependencies** between them. Detailed business logic stays out — that's per-unit Functional Design in Construction.

**Context loaded**:
- [requirements.md](../requirements/requirements.md) — 29 FRs across 13 groups, 7 NFR groups
- [stories.md](../user-stories/stories.md) — 20 stories, 3 personas
- [execution-plan.md](execution-plan.md) — risks called out

---

## Design Questions

These are the genuine decisions that shape the component layout. Recommendations are based on Stardew/SMAPI community norms and the maintainability bar set in NFR-MAINT-01 / S-19.

### Question D1 — Pure-logic separation strategy

Where does the pure logic (rate calc, deposit/refund, zone math, capability eval, save DTOs) live relative to SMAPI-bound code?

A) **Separate project** (e.g., `Dayswork.Core` referenced by `Dayswork`) with **zero** SMAPI/StardewValley assembly references — guarantees you cannot accidentally couple pure logic to the runtime; tests in `Dayswork.Tests` reference only `Dayswork.Core`
B) **Single project, separated by namespace** (e.g., `Dayswork.Domain.*` vs `Dayswork.Integration.*`) — simpler solution structure; relies on discipline to avoid using SMAPI types in `Domain`
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **A**. Mechanically enforces NFR-MAINT-01/03 and makes S-19's PBT obligations trivial to satisfy. The cost is one extra project file. The benefit is "can it compile?" answers "did I just pollute the pure layer?"

---

### Question D2 — Dependency injection / composition

How should component dependencies be wired?

A) **Hand-wired composition root** in `ModEntry.Entry()` — a few constructor calls, no DI container; standard for small-to-medium SMAPI mods
B) **Microsoft.Extensions.DependencyInjection container** — familiar from ASP.NET, scales well, slight overhead and a new dependency
C) **Static service locator** (a static class exposing singletons) — easy to use but harder to test and a known antipattern
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **A**. SMAPI mods are small; a container is overkill. Hand-wired composition is explicit, debuggable, and adds no dependencies. Easy to test because each component takes its dependencies via constructor.

---

### Question D3 — Shift orchestrator pattern

How should the worker's shift be modeled?

A) **Explicit state machine** (states: `WaitingForSpawn → Working → Stuck → Recovering → Depositing → Exiting → Done`) with transitions driven by the SMAPI `UpdateTicked` event — testable, debuggable, matches the spec's stuck-escalation language
B) **Imperative update() loop** with nested if/else inside the worker NPC's `update()` override — closer to how vanilla Stardew code works; harder to test
C) **Coroutine-style** using `IEnumerator` (Unity-ish pattern) — expressive for sequencing but unidiomatic for SMAPI mods; harder for newcomers to read
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **A**. The shift has many discrete phases with non-trivial transitions (stuck recovery, festival skip, sleep fast-forward). A state machine makes those transitions explicit, testable in isolation (the state-transition function is pure), and easy to extend if v2 adds more states. The state-machine engine itself becomes part of `Dayswork.Core`.

---

### Question D4 — Configuration access pattern

How should components read tunable config values (rates, average-speed constant, stuck thresholds)?

A) **Inject an immutable `IConfigSnapshot` into each component that needs config** — captured per-contract or per-shift; mid-shift config changes don't take effect until next shift (matches FR-PAY-08)
B) **Inject a live `IConfigProvider`** that always returns current values — components see config changes immediately
C) **Static config singleton** — easiest to use, hardest to test, doesn't enforce snapshot semantics
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **A**. FR-PAY-08 already mandates snapshot semantics ("new rates apply next morning"). Modelling that with an immutable snapshot makes the requirement self-documenting at the type level and tests for rate calc become trivial (no mocking of a live provider).

---

### Question D5 — Cross-component eventing

Some events are interesting to multiple components (e.g., "shift ended" matters to refund calc, mail dispatcher, NPC despawn, save persistence). How should those fan out?

A) **In-process event bus** (a tiny pub/sub class in `Dayswork.Core`) — components subscribe at composition; orchestrator publishes events
B) **Direct method calls** from the orchestrator to each affected component, in a fixed order — explicit, simple, harder to extend
C) **SMAPI's own events for everything** (`DayEnding`, `Saving`, `TimeChanged`) — relies on SMAPI's lifecycle entirely; can't represent mod-internal events
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **B** for v1. The number of fan-out events is small and stable (shift started, shift ended, deposit overflowed, contract created/cancelled). An event bus is the right move at v2 if subscribers proliferate. For v1, direct method calls in a documented order keep the orchestrator readable for someone new to the codebase.

---

### Question D6 — UI menu structure

The four hiring screens — one big menu class with internal screen state, or four small `IClickableMenu` subclasses?

A) **Four separate `IClickableMenu` subclasses** with a thin coordinator that hands off between them (`HiringFlowCoordinator`) — each screen is independently testable / readable / replaceable
B) **One `HireFarmhandMenu : IClickableMenu`** with an internal `currentScreen` enum and conditional render/handle logic — fewer files; common Stardew pattern for small flows
X) Other

[Answer]: A (recommendation accepted)

> Recommendation: **A**. Four screens with substantial individual responsibility (Screen 2 alone has zone draw mode + chest assignment dropdown — a screen unto itself). Separate classes also make gamepad-focus management cleaner.

---

## Plan Checklist (executes after approval)

When you approve, Part 2 will generate these artifacts in `aidlc-docs/inception/application-design/`:

- [x] `components.md` — every named component with purpose, responsibilities, public interface, and which project (Core vs Mod) it lives in
- [x] `component-methods.md` — method signatures for each component's public interface; brief purpose + I/O types per method (no business logic — that's Functional Design)
- [x] `services.md` — orchestration services (shift orchestrator, hiring-flow coordinator, mail dispatcher, etc.) and how they sequence component calls
- [x] `component-dependency.md` — dependency matrix + data-flow diagram (Mermaid + text fallback)
- [x] `application-design.md` — consolidated overview tying all of the above together with a high-level architecture diagram
