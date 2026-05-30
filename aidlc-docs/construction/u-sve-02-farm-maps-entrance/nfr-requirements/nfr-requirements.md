# NFR Requirements — U-SVE-02 SVE Farm Maps + Worker Entrance

Quality bar for entrance resolution, mapped to the change-level `NFR-SVE-*`. Answers: Q1=A (on-demand signature), Q2=A (fallback to heuristic).

## Performance
- **NFRU2-01 (→ NFR-SVE-06) On-demand signature.** The farm-map signature is computed only at the 6am spawn and at shift exit (a handful of times per shift), never in a per-tile or per-frame path. No caching is required; cost is negligible.

## Reliability / Resilience
- **NFRU2-02 (→ NFR-SVE-04) Always returns a tile.** If the live map is unavailable or signature extraction fails, entrance resolution falls back to the existing `Farm.warps` heuristic + `(77,15)` and never throws. The override is best-effort.

## Determinism & Correctness
- **NFRU2-03 (→ NFR-SVE-03) Deterministic lookup.** The signature→tile lookup and the override-first precedence are pure, deterministic functions.
- **NFRU2-06 (→ NFR-SVE-03) Grounded data.** Every map signature and entrance tile is verified from SVE source and confirmed by playtest before encoding; nothing assumed.

## Isolation / Vanilla invariance
- **NFRU2-04 (→ NFR-SVE-01) Vanilla unchanged.** A vanilla farm's signature is absent from the SVE table, so entrance resolution is byte-for-byte the existing heuristic.

## Testability
- **NFRU2-05 (→ NFR-SVE-05) Pure logic PBT-tested.** The signature→tile lookup and override-first precedence are covered by xUnit + FsCheck without SMAPI. Live-map signature extraction (the only SMAPI-touching part) is validated via manual SVE playtest per supported map.

## Security
- **N/A** — no security surface.

## Extension Compliance

| Extension | Status | NFR-requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Compliant — NFRU2-03/05 set the FsCheck obligations (lookup determinism, override precedence) carried into NFR Design and Code Generation. |
