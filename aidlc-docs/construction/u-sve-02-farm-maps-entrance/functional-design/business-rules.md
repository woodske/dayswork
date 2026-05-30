# Business Rules — U-SVE-02 SVE Farm Maps + Worker Entrance

## Identity

### BR-SVE2-01 Identity by live-map signature
The active farm map is identified by a signature computed from the **live** `Farm.Map`, never by location name (all SVE farms present as `"Farm"`) and never by installed mod id (multiple farm-map packs may be installed simultaneously).

### BR-SVE2-02 Signature definition
The signature is the farm map's `(width, height)` plus an optional unique map-property discriminator used only when dimensions are not unique among supported maps. The exact dimensions and any discriminator are verified from each SVE map's source (BR-SVE2-05).

### BR-SVE2-03 Robust to multiple farm-map packs
Because the signature reflects the map Content Patcher actually applied to `Maps/Farm`, identity is correct whether one or several farm-map packs are installed.

## Entrance resolution

### BR-SVE2-04 Override-first, else heuristic
Entrance resolution consults the per-signature override first. If there is no override for the active signature, the existing `Farm.warps` heuristic and `(77,15)` fallback are used unchanged (FR-SVE-06).

### BR-SVE2-05 No assumed coordinates or signatures
Every entrance tile and every map signature is verified from the SVE map source and confirmed by manual SVE playtest before being encoded. Nothing is guessed (NFR-SVE-03).

### BR-SVE2-06 Unknown signature is a graceful pass-through
A farm whose signature is not in the table (vanilla, an unsupported expansion farm, or a future SVE map) yields no override and falls through to the heuristic. Never crash.

### BR-SVE2-07 Consistent arrival and departure
The same resolved entrance is used for both the 6am spawn and the shift-end exit.

## Preservation

### BR-SVE2-08 Vanilla unaffected
On a vanilla farm the signature is absent from the SVE table, so entrance resolution is byte-for-byte the existing heuristic. No vanilla behavior changes (NFR-SVE-01).

### BR-SVE2-09 Unreachable tiles still skipped
Unreachable zone/approach tiles on SVE maps are skipped as today (FR-SVE-15); the entrance approach search reuses the existing passability logic.

### BR-SVE2-10 No new persistence or config
This unit adds no saved data and no player-facing configuration.

## Testable properties (PBT — FsCheck; full mode)

| Rule | Property category | Property |
|---|---|---|
| BR-SVE2-04 | Invariant | When the active signature has an override, resolution returns it; otherwise it returns the heuristic result (override strictly takes precedence). |
| BR-SVE2-01/02 | Invariant | Signature lookup is a pure, deterministic function of `(width, height, discriminator)`; equal signatures map to equal results. |
| BR-SVE2-06/08 | Invariant | A signature not present in the table yields "no override" (vanilla and unknown farms are pass-through). |

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security behavior. |
| Property-Based Testing | Enabled, full | Compliant — the signature→override lookup and override-first precedence are pure and carry FsCheck properties into Code Generation; live-map signature extraction is exercised via example/manual playtest. |
