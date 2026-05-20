# U-13 — Worker Features: NFR Design Plan

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability
**Phase**: CONSTRUCTION — NFR Design

---

## Plan Checklist

- [x] Analyze NFR requirements
- [x] Select design patterns (retained + new)
- [x] Resolve the deferred TS-U13-04 draw/update integration choice
- [x] Define logical components + responsibilities
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [ ] Present completion message and await approval

---

## Assessment — no blocking user questions

Pattern selection follows directly from the approved NFR Requirements. The single open engineering choice carried over — **TS-U13-04 worker draw/update integration** — is an internal pattern decision (not a product/UX preference), resolved here as **manual render hook with Y-depth sort**, keeping the Farmer out of all game-managed/serialized collections (honors SAFE-U13-03). The BR-WORKER-03 cosmetic fallback remains available; final confirmation is a code-generation play-test point. This mirrors how U-10's NFR Design was completed directly.

- **Resilience**: stuck escalation (bounded), reachable-teleport validation, skip-and-continue, classifier-never-throws.
- **Performance**: retained Throttled-Tick + Once-Per-Shift Scan; per-frame single-Farmer draw.
- **Security/Scalability**: N/A (single-player mod; Security Baseline disabled).
- **Logical components**: extended orchestrator + state machine; new StuckDetector (Core); Farmer-based worker, movement driver, ToolSwapAnimator, render hook, hit-reaction watcher, object classifier, appearance randomizer (Mod).
