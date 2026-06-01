# NFR Requirements Plan — U-SVE-02 SVE Farm Maps + Worker Entrance

**Stage**: CONSTRUCTION → U-SVE-02 → NFR Requirements (Part 1: plan + questions).

**Context**: NFRs are largely inherited from the change-level `NFR-SVE-*` and the U-SVE-02 functional design. Two confirmations specific to entrance resolution; defaults reflect existing project conventions.

## Question 1 — Signature computation locality / performance
A) **(Recommended)** Compute the farm-map signature **on demand only at spawn and shift-exit** (a handful of times per shift) — it is not in any per-tile/per-frame path, so cost is negligible and no caching is needed (NFR-SVE-06).
B) Cache the computed signature per loaded farm/session.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Reliability if the signature cannot be computed
A) **(Recommended)** If the live map is unavailable or signature extraction fails, **fall back to the existing warp heuristic** (never throw) — the override is best-effort and entrance resolution must always return a tile (NFR-SVE-04).
B) Treat a signature-extraction failure as a hard error.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (artifact generation — after answers resolved)

- [x] Create `construction/u-sve-02-farm-maps-entrance/nfr-requirements/nfr-requirements.md` (performance/locality, reliability fallback, determinism of lookup, testability/PBT, vanilla invariance — mapped to NFR-SVE-*).
- [x] Create `.../nfr-requirements/tech-stack-decisions.md` (reuse existing stack; no new deps; signature extraction in the Mod adapter, table/lookup pure in Core).
- [x] Extension compliance (Security N/A; PBT full).
- [x] Update `aidlc-state.md` and append to `audit.md`.
