# U-05 Pricing Core — NFR Design Plan

**Unit**: U-05 — Pricing Core
**Stage**: NFR Design
**Status**: Complete

**Context loaded**:
- `nfr-requirements.md` — NFR-SAFE-02, NFR-MAINT-03, PBT-03, PBT-07 are the four blocking obligations
- `business-logic-model.md` — Math.Ceiling + Math.Clamp already specified in functional design

## Category assessment (why no questions are needed)

| Category | Assessment |
|---|---|
| Resilience Patterns | N/A — no I/O, no external calls, nothing to retry or recover |
| Scalability Patterns | N/A — O(10 tasks) / O(zones) per call, called at most once per morning |
| Performance Patterns | N/A — sub-microsecond arithmetic; no optimization warranted |
| Security Patterns | N/A — Security Baseline disabled (NFR-SEC-01) |
| Logical Components | Two patterns apply: Gold Integrity and Pure Function Isolation; no infrastructure components (queues, caches, etc.) |

All NFR design is directly derived from the functional design decisions already approved. No user input needed.

## Plan Checklist

- [x] Analyze NFR requirements artifacts
- [x] Assess all five NFR design categories
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
