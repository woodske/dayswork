# Logical Components — U-SVE-01 Expansion-Compatibility Provider Foundation

Maps the NFR patterns onto the components from [sve-compatibility-application-design.md](../../../inception/application-design/sve-compatibility-application-design.md). No new infrastructure components (no queues, caches beyond the in-memory cached profile, circuit breakers, or external services) — this is a local SMAPI mod.

## Component responsibilities under NFR

| Component | Layer | NFR role | Patterns |
|---|---|---|---|
| `C-19 IExpansionProfile` | Core (pure) | Deterministic override lookups; contract for extensibility | P-SVE-03, P-SVE-05 |
| `C-20 ExpansionProfileSelector` | Core (pure) | Deterministic, ordered strategy selection; Vanilla fallback | P-SVE-05, P-SVE-03 |
| `C-21 VanillaExpansionProfile` | Core (pure) | Null-Object guaranteeing vanilla invariance | P-SVE-04 |
| `C-22 SveExpansionProfile` | Core (pure) | Centralized SVE identifiers (tables empty in this unit) | P-SVE-05 |
| `C-23 AnimalBuildingCapacityPolicy` | Core (pure) | Total, deterministic clamp-based capacity | P-SVE-06 |
| `M-22 ExpansionDetector` | Mod (adapter) | Guarded one-time detection + log once | P-SVE-01 |
| `M-23 ExpansionCompatService` | Mod (adapter) | Cached singleton seam; gathers live inputs, forwards to pure layer | P-SVE-02, P-SVE-03 |
| `M-01 ModEntry` | Mod (composition root) | Builds + caches the seam at `GameLaunched`; injects into consumers | P-SVE-01, P-SVE-02 |

## Runtime data flow (NFR view)

```
GameLaunched (once)
  -> M-22 ExpansionDetector  [guarded; P-SVE-01]
       IModRegistry.IsLoaded(...) -> installed-id set
       C-20 Select(...) -> active C-19 profile (C-21 or C-22)
  -> construct + cache M-23 ExpansionCompatService  [singleton; P-SVE-02]
  -> inject into ShiftOrchestrator / AnimalTaskHandler / ObjectTargetClassifier / building navigators

Per shift / per tile (hot path)
  -> consumer calls cached M-23 (constant-time)
       -> forwards to pure C-19 / C-23  [P-SVE-03]
       -> Vanilla profile or empty-table SVE profile -> "no override" -> existing behavior  [P-SVE-04]
```

## Failure modes & handling

| Failure | Handling | Pattern |
|---|---|---|
| Mod registry query throws / odd result | Guard → log warning → Vanilla profile | P-SVE-01 |
| Profile construction error | Guard → Vanilla profile | P-SVE-01 |
| Capacity inputs out of range | Clamp to `[0, MaxOccupants]`; never throw | P-SVE-06 |
| Unknown content / location (later units) | Lookup returns "no override" → existing skip | P-SVE-04 |

## No persistence / infrastructure changes
- The active profile is runtime-derived and session-cached; nothing is persisted.
- No new external infrastructure components are introduced.

## Extension Compliance

| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Pure components (C-19/C-20/C-23) carry the FsCheck obligations into Code Generation. |
