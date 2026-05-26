# U-21 — Worker Energy + Shift Runtime Refresh: NFR Design Plan

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-21`. See [nfr-requirements/](../u-21-worker-energy-shift-runtime-refresh/nfr-requirements/).

---

## Plan Checklist

- [x] Analyze NFR requirements artifacts
- [x] Create this NFR design plan
- [x] Evaluate all NFR design question categories and determine whether clarification is needed
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [x] Present completion message and await approval

---

## Pattern Determination

No additional user questions are needed for U-21 NFR Design. The approved NFR requirements and functional design already determine the pattern set cleanly:

- **Resilience patterns** — Applicable and already determined by the approved stop-and-settle reliability bar:
  - unified wrap-up path across normal completion, zero stamina, 8pm, sleep-stop, and stuck abort
  - reuse of the existing output/deposit safety pipeline
  - removal of refund/debt settlement from active runtime stop logic
- **Scalability patterns** — N/A. This is a local single-worker in-process runtime seam with no distributed load, replica, queue, or scale-out mechanism.
- **Performance patterns** — Applicable and already determined:
  - lightweight per-tick orchestration
  - synchronous per-beat stamina application
  - explicit config-driven pacing instead of expensive runtime drag
  - no async HUD/event/job infrastructure
- **Security patterns** — N/A. Security Baseline is disabled project-wide and the unit has no network/auth/PII surface.
- **Logical components** — Applicable and already determined:
  - pure Core ownership of energy arithmetic and work-boundary stop rules
  - `ShiftOrchestrator` as the live world adapter rather than the source of truth for stamina logic
  - a small pacing profile / presentation seam for slower readable worker behavior
  - dedicated U-21 test-side helpers for invariants and state-sequence coverage

The approved NFR requirements are all recommended-path decisions (`NFR-Q1=A` through `NFR-Q5=A`), so no clarification round is needed to resolve tradeoffs before producing the NFR design artifacts.

---

## Artifact Output

- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/nfr-design/logical-components.md`
