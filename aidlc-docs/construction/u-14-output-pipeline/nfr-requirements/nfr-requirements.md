# U-14 — NFR Requirements

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail

Inherits U-10/U-13's worker NFRs; adds requirements for multi-trip deposit, chest-fallback reliability, overflow mail, and the MFM dependency. Decisions FD-Q1..Q7 = A apply throughout.

---

## Safety & Data Integrity

### SAFE-U14-01 — No items lost: conservation across deposit + mail (NFR-SAFE-01)
This is the unit's defining guarantee. Every item the worker buffers is delivered to exactly one of: an assigned chest (fully or partially), the shipping bin, or the next-morning overflow mail. The set {deposited} ∪ {mailed} always equals {collected}. No path drops, double-counts, or silently discards items — including the chest-full, chest-missing, unassigned, 8pm-cap, stuck-early-end, and sleep-interruption paths. *(BR-SAFE-01, BR-OUT-05..09, BR-INT-01)*

### SAFE-U14-02 — Integer-clamped refund unchanged (NFR-SAFE-02)
U-14 does not alter refund math. The refund is still `clamp(deposit − hoursWorked × rate, 0, deposit)` applied at exit with integer arithmetic; deposit-run walking is not billed. *(BR-OUT-11)*

### SAFE-U14-03 — No new persisted Dayswork data (NFR-SAFE-03)
Per FD-Q4=A, overflow and warning letters are queued for next-morning delivery via the platform/MFM "deliver tomorrow" mechanism. U-14 introduces **no** new Dayswork-namespaced save structure, so it adds no new save round-trip surface and cannot corrupt save data through its own serialization. Mail persistence is owned by Stardew/MFM. *(FD-Q4=A)*

### SAFE-U14-04 — Worker still collects only self-caused drops (NFR-SAFE-04)
Unchanged from U-13B: only debris the worker's own actions create is buffered. U-14 changes *routing*, not *what* is collected. *(retained)*

---

## Performance

### PERF-U14-01 — Deposit planning is a one-time, shift-end cost (NFR-PERF-02)
`DepositPlanner.Plan` runs **once** when the shift ends. Cost is O(n) over buffered items to resolve+group, plus O(d²) nearest-neighbor over `d` distinct destinations where `d` is tiny (a handful). Negligible and off the per-frame path. *(BR-OUT-03/04)*

### PERF-U14-02 — Chest resolution is per-trip, not per-frame (NFR-PERF-01)
`ChestResolver.ResolveChest` is called once per chest trip, at arrival — at most `d` lookups per shift. No per-frame chest scanning. The existing UpdateTicked throttle (every 4th tick) from U-10/U-13 is retained for the deposit-walk loop. *(NFR-PERF-01)*

### PERF-U14-03 — Mail dispatch is once per shift (NFR-PERF-01)
At most one overflow letter and one warning letter are queued per shift, at exit. No repeated mail work. *(BR-MAIL-01/05)*

---

## Usability

### UX-U14-01 — All mail strings localizable (NFR-UX-02)
U-14 adds new user-visible strings: the sender label and the overflow/warning bodies. All are routed through `I18nHelper` / `i18n/default.json` — no hardcoded user-visible text. New keys: `mail.sender`, `mail.overflow.chest_full`, `mail.overflow.chest_missing`, `mail.overflow.no_chest_assigned`, `mail.overflow.not_delivered`, `mail.warning.tool_missing` (plus any per-task-name keys the warning body enumerates). *(BR-MAIL-03/04, FD-Q6=A, FD-Q7=A)*

---

## Reliability

### REL-U14-01 — Chest-missing degrades gracefully
A null result from `ChestResolver.ResolveChest` is an expected, handled case: the trip's items move to overflow (reason `ChestMissing`) and the worker continues remaining trips. No exception, no shift abort. *(BR-OUT-06, FR-OUT-03)*

### REL-U14-02 — Chest-full degrades gracefully
A partially-fitting chest deposits what fits; the remainder moves to overflow (reason `ChestFull`). No item is dropped on the ground or lost. *(BR-OUT-07, FR-OUT-02)*

### REL-U14-03 — Exactly one overflow letter, even with mixed reasons
Regardless of how many chests failed or how many reasons applied, at most one overflow letter is queued per shift, carrying the union of all overflow items. *(BR-MAIL-01, S-11)*

### REL-U14-04 — Large overflow attachment is bounded and tolerated
Overflow volume is bounded by a single day's *undeliverable* drops (the exception path, not the norm). The MailDispatcher must hand MFM the full attachment set without truncating items (NFR-SAFE-01 wins over tidiness). If MFM imposes a practical per-letter attachment limit, the handling pattern (single letter best-effort vs. splitting) is resolved in NFR Design / Code Generation; the **product** rule remains "one letter, all items". *(deferred engineering detail)*

### REL-U14-05 — MFM acquisition failure is logged, never crashes
MFM is a required dependency (SMAPI blocks load without it), so `GetApi` is expected to succeed. If the API is unexpectedly unavailable at runtime, the dispatcher logs an error and the shift completes without crashing; item-safety is already covered because items remain in the buffer/mail intent rather than being discarded mid-flight. Exact fallback is an NFR-Design decision. *(deferred engineering detail)*

---

## Maintainability

### MAINT-U14-01 — DepositPlanner is pure Core (NFR-MAINT-03)
`C-11 DepositPlanner` lives in `Dayswork.Core/Inventory/` with **zero** Stardew/SMAPI references. Game distances enter via an injected `Func<TileCoord,TileCoord,int>` oracle; chest liveness is resolved by the Mod layer, not the planner. This keeps the planner the PBT target. *(BR-OUT-03/04)*

### MAINT-U14-02 — Stardew refs confined to the Mod layer (NFR-MAINT-03)
`M-16 MailDispatcher`, the `ChestResolver` calls, and the multi-trip orchestration live in `Dayswork` and hold all Stardew/SMAPI/MFM references behind interfaces (`IMailDispatcher`). The `ItemBuffer` extension (adding `SourceTask`) stays pure Core.

### MAINT-U14-03 — No new Harmony patches (NFR-MAINT-04)
U-14 introduces no Harmony patches; mail goes through the MFM API and the deposit loop through existing SMAPI events. *(NFR-MAINT-04 N/A for new patches.)*

### MAINT-U14-04 — .NET conventions (NFR-MAINT-05)
Code follows standard .NET conventions (`dotnet format`).

---

## Compatibility

### COMPAT-U14-01 — MFM declared as a required dependency (NFR-COMPAT-04)
`manifest.json` gains MFM (DIGUS' Mail Framework Mod) as a required `Dependencies` entry (UniqueID + minimum version confirmed at Code Generation). SMAPI surfaces a clear missing-dependency message to players who lack it. *(BR-DEP-01, V9)*

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

PBT-03 is **blocking** for the pure planner; PBT-07 (shared generator) and PBT-08 (seed logging) are blocking. **PBT-02 is N/A** — FD-Q4=A introduces no new round-trip serialization type.

### PBT-U14-01 — Conservation (PBT-03 blocking)
For any buffer snapshot + assignment map, the multiset of items across `DepositPlan.Trips[*].Items` ∪ `PreMailedOverflow` equals the input snapshot. *(SAFE-U14-01, BR-SAFE-01)*

### PBT-U14-02 — Trip count = distinct walkable destinations (PBT-03 blocking)
`Trips.Count` equals the number of distinct chest+bin destinations present in the resolved buffer; mail-bound items contribute no trip. *(BR-OUT-03/08)*

### PBT-U14-03 — No empty or mail trips (PBT-03 blocking)
Every `DepositTrip.Items` is non-empty and no trip targets `MailDestination`. *(BR-OUT-03/08)*

### PBT-U14-04 — Resolution totality (PBT-03 blocking)
Every buffered item resolves to exactly one outcome (a specific chest trip, the bin trip, or pre-mailed); no item is unresolved or double-resolved, for any assignment map (including maps missing keys → mail). *(BR-OUT-02)*

### PBT-U14-05 — Shared generator (PBT-07 blocking)
A reusable FsCheck generator produces `(IReadOnlyList<BufferedItem>, IReadOnlyDictionary<TaskKind,DestinationKey>)` pairs for the planner properties, composing the existing `TaskKind` and `ChestRef`/`Zone` generators. *(PBT-07)*

### PBT-U14-06 — Seed logging (PBT-08 blocking)
All new U-14 properties follow the U-02 seed + shrunk-input logging convention. *(PBT-08)*

**Not PBT (unit-tested instead):** the nearest-neighbor *ordering quality* is a sanity unit test (not a hard property, per unit-of-work.md); chest-full/chest-missing execution and MailDispatcher are Mod-layer integration/play-tested, since they read live game state and the MFM API.

---

## Security
Security Baseline extension is **disabled** project-wide (NFR-SEC-01): no network, PII, auth, or external-input surface. All Security Baseline rules are **N/A** for U-14.
