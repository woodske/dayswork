# U-13B — Farmer Worker + Tool Visuals: NFR Design Plan

**Unit**: U-13B — Farmer Worker + Tool Visuals
**Phase**: CONSTRUCTION — NFR Design

---

## Plan Checklist

- [x] Analyze NFR requirements artifacts
- [x] Resolve deferred tech decision TS-U13B-01 (movement smoothness cadence)
- [x] Define NFR design patterns (own Patterns F/G from U-13 split; add appearance + tool-swap + per-tick-movement patterns)
- [x] Map logical components
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

Consistent with how U-13 NFR Design was handled (it resolved TS-U13-04 directly), all U-13B NFR-design choices are engineering/pattern decisions, not product preferences:

- **Resilience**: inherited unchanged (skip-and-continue, save-exclusion, behaviour preservation); no new failure modes beyond the entity/movement/draw swap.
- **Scalability**: N/A — single-player mod.
- **Performance**: the only open call was TS-U13B-01 (movement smoothness cadence), resolved below.
- **Security**: N/A — Security Baseline disabled (Q28).
- **Logical components**: fully determined by the approved Functional Design + ownership matrix.

### TS-U13B-01 resolved → per-tick position stepping + throttled decision logic

The worker's `Position` is advanced **every** `UpdateTicked` (~60 Hz) so the walk looks smooth, while the heavier decision logic (work-list dispatch, stuck sampling, hit detection) stays on the existing **every-4th-tick** throttle (~15 Hz). Position stepping is O(1) (move toward the current waypoint by the per-tick distance), so running it every tick is negligible and avoids the ~4 px "stutter" that throttled stepping would cause against a 60 fps draw. Render-side interpolation (option c) is rejected as unnecessary complexity once position itself updates per tick. Arrival is detected on the throttled tick (≤~66 ms latency — imperceptible). Final confirmation is a code-generation play-test point. *(Resolves TS-U13B-01; satisfies PERF-U13B-01/03.)*

---

## Patterns (detail in nfr-design-patterns.md)
- **Pattern F — Farmer-as-Worker Rendering** (owned here; manual `Display.RenderedWorld` hook, on-top draw per FD-Q2=A, BR-WORKER-03 fallback).
- **Pattern G — Manual Path-Follow Movement** (owned here; A* path-compute-only per FD-Q1=A, per-tick position stepping per TS-U13B-01).
- **Pattern J — Contract-Seeded Appearance** (deterministic randomization, FD-Q3/Q4=A).
- **Pattern K — Pure Tool Map + Mod-side Swing** (Core `WorkerTool` + `ToolSwapAnimator`, instant swap FD-Q6=A, hand-pick beat FD-Q5=A).
- **Pattern H — Inherent Invulnerability + Swing-Proximity Emote** (carried from U-13; entity reference now `Farmer`).
- **Pattern I — Save-Exclusion Guard** (carried; Farmer never serialized).
- Retained U-10/U-13: Throttled-Tick, Invoke-and-Poll, Skip-and-Continue, Core-Purity, behaviour-preservation.
