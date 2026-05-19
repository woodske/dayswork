# Unit of Work Plan — Dayswork

**Status**: ✅ Part 1 complete (plan approved 2026-05-18). ✅ Part 2 complete — three unit artifacts generated.

**Scope**: Decompose the 35 components from [components.md](../application-design/components.md) into logical "units of work" — each unit is a coherent batch of code that goes through the Construction per-unit loop (Functional Design → NFR Requirements → NFR Design → Code Generation) before the next one starts.

**Context loaded**: [requirements.md](../requirements/requirements.md), [stories.md](../user-stories/stories.md), [application-design.md](../application-design/application-design.md), [design-verification-notes.md](../application-design/design-verification-notes.md), [execution-plan.md](execution-plan.md).

**Things already decided** (no need to re-ask):
- Solo developer, no team coordination concerns
- Single deployable artifact (one mod assembly + assets, distributed as one Nexus zip)
- Solution layout = 3 projects (`Dayswork`, `Dayswork.Core`, `Dayswork.Tests`) — D1
- 35 components + 6 services already inventoried in Application Design
- Source spec's "Suggested build order" lists 12 candidate sequence points; the execution plan estimated ~12–17 units total

---

## Planning Questions

### Question U1 — Unit slicing axis

How should units be sliced relative to the Core ↔ Mod project boundary?

A) **Vertical feature slices** — each unit owns both its Core pieces AND its Mod pieces AND its tests (e.g., the "Rate calculation" unit ships `Dayswork.Core/Pricing/RateCalculator.cs` + xUnit tests + the bits of `HiringFlowCoordinator`/`SummaryMenu` that call it). User-facing functionality lands in one PR per unit.
B) **Horizontal layers** — `Dayswork.Core` is built bottom-up first as a few foundational units, then `Dayswork` (Mod) is built feature-by-feature on top.
C) **Hybrid (Recommended)** — small foundational Core-only units come first for testable primitives (rate calc, deposit/refund, save serialization, state machine). Then vertical feature slices that combine remaining Core + Mod work.
X) Other

[Answer]: (recommendation accepted)

> Recommendation: **C**. Foundational pure-logic primitives are valuable to land + test first (they're the easiest wins and pay back PBT-09's framework setup early). After that, the user-facing features benefit from vertical slicing so each unit produces something demonstrable.

---

### Question U2 — Unit granularity

How many units total? The source spec gestures at 12; the execution plan estimated 12–17.

A) **~12 units** — coarser; some units bundle related Core and Mod pieces (e.g., "Hiring UI" is one unit covering all 4 menus + the coordinator + the overlay)
B) **~16 units (Recommended)** — finer; each major UI screen or worker subsystem is its own unit. Smaller per-unit Construction loop iteration; more checkpoints
C) **~22+ units** — very fine; each individual menu screen, each pure-logic primitive separately. More overhead per unit
X) Other

[Answer]: (recommendation accepted)

> Recommendation: **B**. ~16 units is small enough that each unit's Construction loop finishes in one or two work sessions, but coarse enough to avoid Functional-Design overhead per file. Matches the spec's build-order granularity.

---

### Question U3 — Where does the test project fit?

The PBT obligations live in `Dayswork.Tests`. How should test code map to units?

A) **Tests live in the same unit as the production code they cover** (Recommended) — each Core-unit's Code Generation includes its corresponding test file(s); each Mod-unit's includes whatever light unit tests are practical
B) **One separate "Test infrastructure" unit early** that sets up `Dayswork.Tests` project, FsCheck integration, shared generators, seed-logging conventions — then each later unit drops tests into the established infrastructure
X) Other

[Answer]: (recommendation accepted)

> Recommendation: **B**. There's a real setup cost for the test project (csproj, FsCheck/xUnit packaging, shared FsCheck generators per PBT-07, seed-logging CI pattern per PBT-08). Doing it once upfront as its own unit then having subsequent units just add test files is cleaner than re-inventing each time.

---

### Question U4 — Sequencing strategy

How should units be ordered for the Construction loop?

A) **Dependency-first** — units with no dependencies on later units come first; user-facing features come after their foundations land
B) **Demo-first** — get a minimum end-to-end happy path running early (even with stubs), then fill in depth. E.g., a "Hello bulletin board" unit ships in week 1 that just adds the menu entry that opens a hardcoded "Hi" dialog; everything else replaces stubs
C) **Hybrid (Recommended)** — foundational Core units first (no dependencies), then take a thin end-to-end vertical slice (minimal hire-flow → minimal worker → minimal payment) before deepening each feature
X) Other

[Answer]: (recommendation accepted)

> Recommendation: **C**. Pure dependency-first risks discovering integration issues late. Pure demo-first means re-doing work as scope deepens. The hybrid surfaces integration issues early without throwing away foundational work.

---

### Question U5 — Where does "Project Scaffold" live?

Things like the `Dayswork.sln`, `Dayswork.csproj` with the `<EnableHarmony>true</EnableHarmony>` flag and `ModBuildConfig` NuGet, `manifest.json` skeleton, `i18n/default.json` skeleton, README/LICENSE — these don't map to a user story but are real work.

A) **One "Project Scaffold" unit at the very start** (Recommended) — explicit unit; produces a buildable empty mod that loads in SMAPI with a "Dayswork loaded" log line
B) **Fold scaffold work into the first feature unit** — e.g., the first Core unit includes setting up `Dayswork.sln`; the first Mod unit includes the manifest
C) **Implicit (not a unit; just "do it before everything")** — risks under-thought decisions
X) Other

[Answer]: (recommendation accepted)

> Recommendation: **A**. A loadable empty mod is the smallest thing that proves "Construction is unblocked"; it's the foundation literally everything else rests on. Making it a unit forces a clean checkpoint.

---

## Plan Checklist (Part 2 — runs after approval)

When you approve, Part 2 will generate these artifacts in `aidlc-docs/inception/application-design/`:

- [x] `unit-of-work.md` — full unit list with: ID, name, purpose, components included, stories implemented, code-organization notes
- [x] `unit-of-work-dependency.md` — dependency matrix + sequence diagram (Mermaid + text fallback) showing the per-unit Construction loop order
- [x] `unit-of-work-story-map.md` — mapping from each of the 20 stories in [stories.md](../user-stories/stories.md) to the unit(s) that deliver it
- [x] Validate every story is covered by at least one unit
- [x] Validate every component from [components.md](../application-design/components.md) is owned by exactly one unit
