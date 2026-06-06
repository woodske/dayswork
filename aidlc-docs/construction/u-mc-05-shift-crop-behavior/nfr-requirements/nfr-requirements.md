# U-MC-05 NFR Requirements

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — NFR Requirements
**Status**: Review required

No question round was needed: the approved Functional Design, the feature-level NFR set
(NFR-MC-01..09), and the existing runtime architecture already fix the quality bar. The
recommended posture is recorded below (consistent with the user's standing pre-authorization
to use recommended options).

## NFR-MC5-01 — Determinism (PBT full mode)
All decision logic — viability, seed/fertilizer atomicity, per-tile action ordering,
supply bounding, the action→tool/energy mapping, and the managed-zone tile-exclusion
predicate — is **pure and deterministic** in `Dayswork.Core`. Same inputs always yield the
same plan and mappings. The runtime adapter contains no decision logic beyond reading live
state and applying the pure plan. (NFR-MC-01)

## NFR-MC5-02 — Performance
Per-shift planning and per-tile checks keep the existing synchronous tick loop responsive:
- The field reader snapshots managed-zone tiles once per batch (and re-reads only the
  active zone as needed); cost is O(managed tiles), not farm-wide.
- No per-tile graph discovery; navigation reuses the existing bounded route helpers.
- Beat cadence is the existing `WorkerActionAnimationMs` knob; the `4`-tick throttle and
  work-unit-boundary model are unchanged. (NFR-MC-02)

## NFR-MC5-03 — Item & gold safety
- Seeds/fertilizer consumed are exactly those carried from the input chest; **leftover
  carried supply returns to the input chest at end of shift** — never lost.
- Harvested output is held in worker inventory and settled to the output chest via the
  existing deposit/overflow pipeline; nothing is dropped or duplicated.
- This unit performs **no wallet mutation** (no shopping); gold safety is inherited and
  unaffected. (NFR-MC-03)

## NFR-MC5-04 — Vanilla / no-SVE invariance
With no managed crop plan, behavior is byte-for-byte unchanged. The managed-crop batch
operates only on the open farm this unit; greenhouse/shed degrade cleanly because they are
simply not emitted (U-MC-07 adds them). SVE absence changes nothing. (NFR-MC-04)

## NFR-MC5-05 — Resilience
Missing/under-leveled tools, unavailable fertilizer, partial stock, non-diggable tiles, and
unreachable tiles are handled by **skip + (where player-relevant) notify** — never throwing
or aborting the shift. A managed-crop batch that yields no actionable work is skipped like
any empty batch. (NFR-MC-05)

## NFR-MC5-06 — Backward-compatible persistence
U-MC-05 adds **no** save-schema change. It reads the existing V3 `CropPlan` (incl. the
already-persisted `ClearDebrisBeforeTilling`/`ClearDeadPlants` flags and per-season
`AutoReplant`). New energy costs live in config/GMCM, not the save. (NFR-MC-06)

## NFR-MC5-07 — i18n
All new player-facing text (HUD notices, the two toggle labels, GMCM cost labels) is
i18n-backed and passes the hardcoded-string lint gate. (NFR-MC-07)

## NFR-MC5-08 — Test rigor
- FsCheck properties (full mode) for the new pure seams (action→tool/energy mapping totality
  and determinism; managed-zone tile-exclusion disjoint partition) plus the carried-forward
  U-MC-01 planner properties (ordering, atomicity, viability, null-stock supply bounding).
- xUnit examples for runtime wiring at the live-API boundary (field reader interpretation,
  capability-skip behavior, supply settle).
- Manual SMAPI playtest closes the unit (authoring → prepare → plant → harvest → replant,
  tool-skip, fertilizer-unavailable, coexistence). (NFR-MC-08)

## NFR-MC5-09 — Tech stack
Reuse the existing C#/.NET 6 + SMAPI + xUnit + FsCheck stack and the existing
navigation/capability/energy/persistence/HUD seams. **No new runtime dependencies.**
(NFR-MC-09)

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A — disabled for Manage Crops (no network/PII/auth surface). |
| Property-Based Testing | Compliant, full mode — PBT-09 remains satisfied by FsCheck.Xunit already present; new pure seams carry blocking PBT obligations. |
