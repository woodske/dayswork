# Domain Entities — U-SVE-02 SVE Farm Maps + Worker Entrance

## Overview

This unit adds a pure farm-map identity value and an entrance-override table to the existing compat seam. No persisted data is introduced.

## Entity: FarmMapSignature (pure, Core)

A stable identifier for a farm map, computed from the live map.

| Field | Meaning |
|---|---|
| `Width` | `farm.Map.Layers[0].LayerWidth`. |
| `Height` | `farm.Map.Layers[0].LayerHeight`. |
| `Discriminator` | Optional string from a unique map property, used only when `(Width, Height)` is not unique among supported maps. Empty when not needed. |

- Pure value type (`readonly record struct`-style), comparable by value.
- The exact `Discriminator` source (if any) is verified per map from SVE source during Code Generation.

## Entity: Entrance override table (in SveExpansionProfile)

A verified mapping from `FarmMapSignature` to the entrance `TileCoord` for supported SVE farms that need an override.

- Keyed by `FarmMapSignature`; value is the verified entrance tile.
- Only contains entries for maps whose heuristic result is wrong (BR-SVE2-04); empty/absent entries fall through to the heuristic.
- Lives in the pure profile so the table + lookup are unit/PBT-testable.

## Seam refinement

`IExpansionProfile.TryGetEntranceOverride` is refined from the U-SVE-01 generic `string farmIdentity` to key on `FarmMapSignature`:

```
bool TryGetEntranceOverride(FarmMapSignature signature, out TileCoord tile)
```

- `VanillaExpansionProfile`: always returns false (no override).
- `SveExpansionProfile`: returns the table entry for the signature if present.
- `ExpansionCompatService.TryGetFarmEntranceOverride(GameLocation farm, out Point tile)` computes the signature from the live map and delegates to the active profile.

## Live-map signature extraction (Mod adapter)

`ExpansionCompatService` builds the `FarmMapSignature` from `farm.Map` (dimensions via `Map.Layers[0]`, optional discriminator via a verified map property). This is the only part that touches live Stardew types; it is thin, with the table/lookup kept pure.

## No persistence changes
No saved data and no player-facing configuration. Identity is recomputed at runtime.

## Frontend/UI artifact
N/A — no menus, screens, controls, or localized strings. The change is visible only through where the worker spawns/exits.

## Testable properties

| Entity / operation | Property category | Property |
|---|---|---|
| Signature→tile lookup | Invariant | Deterministic; equal signatures yield equal results; absent signature → no override. |
| Resolution (override + heuristic) | Invariant | Override result strictly precedes the heuristic result when present. |

## Extension Compliance

| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Pure signature lookup + precedence carry FsCheck properties into Code Generation. |
