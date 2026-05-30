# Business Logic Model — U-SVE-02 SVE Farm Maps + Worker Entrance

## Overview

The worker must spawn at and exit from a sensible entrance on SVE's farm maps (Immersive Farm 2 Remastered, Grandpa's Farm, Frontier Farm). Because SVE farms **replace `Maps/Farm`** (the location stays named `"Farm"`), the active farm is identified by a **signature of the live farm map** — not by location name or installed mod id.

Decisions: FD Q1=**B** (map-signature identity — chosen because farm-map packs can be installed simultaneously; Content Patcher applies only one map to `Maps/Farm`, and the signature reflects the map actually applied), Q2=A (override-first, else heuristic), Q3=A (entrances + signatures verified from SVE source and playtest; nothing assumed).

## Flow 1 — Active farm-map identity (map signature)

```
ResolveFarmMapSignature(GameLocation farm):
  width  = farm.Map.Layers[0].LayerWidth
  height = farm.Map.Layers[0].LayerHeight
  discriminator = (optional) a unique map property, only if width x height is not unique
  -> FarmMapSignature(width, height, discriminator)
```

- Identity is derived from the **live** map, so it is correct regardless of how many farm-map packs are installed (it names the map Content Patcher actually applied).
- Verified dimensions so far (from source): IF2R `163 x 156`, Frontier Farm `156 x 65` (vanilla Standard Farm is `80 x 65`). Grandpa's Farm dimensions and any property tiebreakers are confirmed from source during Code Generation.
- If dimensions alone do not uniquely identify a supported map, a unique SVE map property is added as the tiebreaker — the exact discriminator is verified from each map's source, never assumed.

## Flow 2 — Entrance resolution (override-first, else heuristic)

```
ResolveFarmEntrance(Farm farm):
  if ExpansionCompat.TryGetFarmEntranceOverride(farm, out tile):   # signature lookup
        return tile
  else:
        return FindFarmExitTile(farm)   # existing warp heuristic + (77,15) fallback (unchanged)
```

- `ExpansionCompatService.TryGetFarmEntranceOverride` computes the signature (Flow 1) and looks it up in the active profile's verified signature→tile table.
- The Vanilla profile (and any unmatched signature) returns no override → the existing heuristic runs exactly as today (FR-SVE-06; vanilla invariance).
- A supported SVE map gets an override entry **only if** its heuristic result is wrong in playtest; maps where the heuristic already lands correctly get no entry.

## Flow 3 — Integration points

The same resolved entrance feeds both worker-lifecycle paths that currently call `FindFarmExitTile`:
- **Morning spawn**: the tile where the `FarmhandNpc` spawns at 6am.
- **Shift exit**: the tile the worker walks to before leaving.

Both are routed through the seam so the override (when present) applies consistently to arrival and departure.

## Flow 4 — Graceful behavior

- **Unreachable tiles** on SVE maps (water, cliffs, custom terrain in drawn zones) are silently skipped exactly as today (FR-SVE-15).
- **Unknown/unsupported farm** (signature not in the table): no override → heuristic; never crash.

## Data flow & boundaries

- **Pure (Core)**: the `FarmMapSignature` value and the signature→tile override table + lookup live in the profile (`SveExpansionProfile`), unit/PBT-testable.
- **Mod adapter**: `ExpansionCompatService` computes the signature from the live `farm.Map` and calls the pure lookup; `ShiftOrchestrator` consults the service before its heuristic.
- **No persistence change**: identity is computed at runtime each time; nothing saved.

## Interface note

`IExpansionProfile.TryGetEntranceOverride` (shipped in U-SVE-01 taking a generic `string farmIdentity`) is refined in this unit to key on `FarmMapSignature`. The Mod adapter computes the signature from the live map; the profiles' tables key on it. This is a small, isolated seam extension (the Vanilla profile still returns "no override").
