# Tech Stack Decisions — U-SVE-02 SVE Farm Maps + Worker Entrance

**Decision: reuse the existing stack; no new dependencies.**

| Concern | Decision | Rationale |
|---|---|---|
| Pure logic | `FarmMapSignature` + the signature→tile override table/lookup in `Dayswork.Core/Compat/` | Keeps identity/lookup unit/PBT-testable without SMAPI (NFR-SVE-05). |
| Live-map access | Signature extraction (`Map.Layers[0]` dimensions + optional map property) in `Dayswork/Compat/ExpansionCompatService` | Only the live-object read touches Stardew types; mirrors existing adapter style. |
| Entrance integration | `ShiftOrchestrator.FindFarmExitTile` consults `ModEntry.ExpansionCompat` first, else its existing heuristic | Minimal, isolated change at the established static seam. |
| Testing | xUnit + FsCheck (existing) | Lookup determinism + override precedence properties. |
| New libraries | **None** | Nothing warrants a new dependency. |

## Extension Compliance

| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | FsCheck already in the stack; no change needed. |
