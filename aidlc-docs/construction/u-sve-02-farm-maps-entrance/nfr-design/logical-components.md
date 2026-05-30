# Logical Components — U-SVE-02 SVE Farm Maps + Worker Entrance

Maps the U-SVE-02 patterns onto components. No new infrastructure; no persistence.

| Component | Layer | NFR role | Patterns |
|---|---|---|---|
| `FarmMapSignature` | Core (pure) | Value identity for a farm map | P-SVE2-01 |
| `SveExpansionProfile` entrance table + `TryGetEntranceOverride(FarmMapSignature)` | Core (pure) | Deterministic signature→tile lookup | P-SVE2-02, P-SVE2-03 |
| `VanillaExpansionProfile.TryGetEntranceOverride` | Core (pure) | Always "no override" (vanilla invariance) | P-SVE2-02 |
| `ExpansionCompatService.TryGetFarmEntranceOverride` + signature extraction | Mod (adapter) | Guarded live-map → signature → lookup | P-SVE2-03, P-SVE2-04 |
| `ShiftOrchestrator.FindFarmExitTile` | Mod | Consults the seam first, else existing heuristic | P-SVE2-02 |

## Runtime data flow

```
spawn / exit (per shift)
  -> ShiftOrchestrator.FindFarmExitTile(farm)
       -> ModEntry.ExpansionCompat.TryGetFarmEntranceOverride(farm)
            [guarded] signature = (Map.Layers[0].Width, Height, optional property)
            -> activeProfile.TryGetEntranceOverride(signature) -> tile? 
       -> override tile if present
       -> else existing warp heuristic + (77,15) fallback
```

## Failure modes

| Failure | Handling | Pattern |
|---|---|---|
| Map unavailable / extraction throws | Guard → heuristic result | P-SVE2-04 |
| Signature not in table (vanilla / unknown farm) | "no override" → heuristic | P-SVE2-02 |
| Override tile impassable | Existing approach-tile search / heuristic fallback applies | P-SVE2-02 |

## No persistence / infrastructure changes
Identity is recomputed at runtime; nothing is persisted; no new external components.

## Extension Compliance

| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Pure lookup + precedence carry FsCheck obligations into Code Generation. |
