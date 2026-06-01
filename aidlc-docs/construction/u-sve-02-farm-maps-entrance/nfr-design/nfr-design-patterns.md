# NFR Design Patterns — U-SVE-02 SVE Farm Maps + Worker Entrance

Patterns satisfying the U-SVE-02 NFR requirements (NFRU2-01..06). Scalability/security N/A.

## P-SVE2-01 — Live-map signature identity (Correctness)
Identity is computed from the live `Farm.Map` (`(width, height)` + optional verified map-property tiebreaker), so it names the map Content Patcher actually applied — correct even with multiple farm-map packs installed (NFRU2-03/06; BR-SVE2-01/03).

## P-SVE2-02 — Override-first resolution with heuristic fallback (Strategy/Chain)
Entrance resolution tries the per-signature override first, then chains to the existing `Farm.warps` heuristic + `(77,15)` fallback. Override strictly precedes the heuristic; absence of an override is the common case (FR-SVE-06; BR-SVE2-04).

## P-SVE2-03 — Pure table + thin live extraction (Testability)
The signature→tile table and lookup are pure (`SveExpansionProfile`, testable with FsCheck); only the signature *extraction* from the live map lives in the Mod adapter (`ExpansionCompatService`). Keeps the hard logic testable without SMAPI (NFRU2-05).

## P-SVE2-04 — Guarded extraction → fallback (Resilience)
Signature extraction is guarded; if the map is unavailable or extraction fails, resolution returns the heuristic result and never throws (NFRU2-02). Reuses U-SVE-01's overall fail-safe posture.

## Pattern → NFR map

| Pattern | NFRU2 | NFR-SVE |
|---|---|---|
| P-SVE2-01 | NFRU2-03/06 | NFR-SVE-03 |
| P-SVE2-02 | — (FR-SVE-06) | NFR-SVE-01 |
| P-SVE2-03 | NFRU2-05 | NFR-SVE-05 |
| P-SVE2-04 | NFRU2-02 | NFR-SVE-04 |

## Extension Compliance

| Extension | Status | NFR-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | P-SVE2-02/03 keep lookup + precedence pure for FsCheck. |
