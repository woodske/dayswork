# Unit of Work — Story Map

This document maps each of the 20 user stories in [stories.md](../user-stories/stories.md) to the unit(s) that deliver it. Many stories are delivered by a thin slice in one early unit and then **completed** in a later deepening unit (consistent with the U4 = Hybrid sequencing strategy and the FoundationsThin SliceDeepening structure in [unit-of-work.md](unit-of-work.md)).

**Reading the table**:
- **Primary unit** — where the story's main behavior is first delivered (the unit that "owns" the story for traceability purposes)
- **Completing unit(s)** — later units that finish the story by deepening the initial slice; same column is "—" if the primary unit ships the full story
- **State at unit X** — what the player can actually do at the end of the primary unit's Construction loop; useful for picking smoke-test scenarios

---

## Story-to-unit map

### Section 1 — Discovery & First Hire

| Story | Primary | Completing | State after primary unit |
|---|---|---|---|
| **S-01** Discover the hiring option on the bulletin board | U-08 | — | Bulletin board shows the entry; clicking logs a placeholder message. UI itself lands in U-09. The MP-hidden case also fully works at U-08. |
| **S-02** Configure tasks and see the live hourly rate | U-09 | — | Full story shipped. Toggles render, rate updates in real time, base rate always included, gamepad navigation works. |
| **S-03** Draw zones and select buildings on the farm | U-11 | U-16 deepening | Full story shipped at U-11 (zone drawing + building selection + multi-rectangle + unreachable-tile silent skip + gamepad cursor). U-16 makes selected buildings executable work areas. |
| **S-04** Assign output destinations per task | U-11 | U-14, U-16 deepening | At U-11: chest assignment UI works; rename preserves assignment. At U-14: the orphaned-chest Gherkin case fully fires through the mail fallback. At U-16: building-interior output destinations are exercised by the multi-location deposit run. |
| **S-05** Choose a one-time or recurring schedule | U-09 | U-12 | At U-09: one-time contracts persist through save/load (Gherkin clause). At U-12: schedule selection UI ships and recurring contracts are creatable. |
| **S-06** Review the contract and confirm | U-09 | — | Full story shipped. Summary renders, confirm path deducts deposit, insufficient-gold path is blocked. |

### Section 2 — First Day of Work

| Story | Primary | Completing | State after primary unit |
|---|---|---|---|
| **S-07** Watch the farmhand arrive and work on day one | U-10 | U-13B | At U-10: arrival + walk (teleport stub) to first task tile + placeholder sprite. At U-13: real walking. At U-13B: Farmer worker with visible tool-swap when changing task class. |
| **S-08** Execute tasks in priority order within a zone | U-10 | U-13, U-16 | At U-10: single task executes (one zone, one task type). At U-13: outdoor priority/deepening lands for non-animal tasks. At U-16 Code Generation: animal tasks and building-interior work complete the full priority queue. |
| **S-09** Snapshot tool capabilities at spawn and skip what can't be done | U-10 | U-13 | At U-10: ToolLevelReader runs at 6am and snapshot is captured. At U-13: full capability matrix applied to skip rules (axe-level guards, pickaxe=0 guards, fruit-tree always-skip) plus tool-missing mail warning queued (mail itself delivered by U-14). |
| **S-10** Deposit collected items at shift end | U-10 | U-14 | At U-10: single-trip deposit to shipping bin. At U-14: multi-trip deposit to assigned chests + 8pm-cap-still-deposits + chest-full fallback + chest-destroyed fallback + refund at exit. |
| **S-11** Receive mail for overflow and unassigned output | U-14 | — | Full story shipped. MailDispatcher delivers overflow / chest-missing / no-chest-assigned letters with no fee; shipping-bin-no-overflow holds. |

### Section 3 — Daily Life with a Recurring Contract

| Story | Primary | Completing | State after primary unit |
|---|---|---|---|
| **S-12** Pause, cancel, or edit a recurring contract | U-12 | U-15 | At U-12: Pause/Cancel/Edit UI on the bulletin board; cancel-after-6am-blocked rule enforced; Edit returns to pre-filled menus. At U-15: deposit-deduction-each-morning + can't-afford → cannot-afford mail Gherkin clauses fire. |
| **S-13** Tune rates and constants in GMCM | U-17 | — | Full story shipped. Every spec-listed configurable value editable in GMCM; today's-deposit-uses-R1-tomorrow's-uses-R2 holds. |

### Section 4 — Calendar & Edge Cases

| Story | Primary | Completing | State after primary unit |
|---|---|---|---|
| **S-14** Handle festivals, rainy days, and empty zones without surprise charges | U-15 | — | Full story shipped. Festival skip silent + no deduction; rainy-day Water Crops surcharge excluded by RateCalculator branch (the C-01 rain flag wired up in this unit through CalendarHandlers); empty-zone full-refund per FR-PAY-06. |
| **S-15** Player sleeps before the farmhand finishes — shift stops and settles atomically | U-15 | — | Full story shipped. Sleep-confirm stops the worker, mails collected-but-undelivered items and refund, and lands settlement in *today*'s state before day-rollover. |
| **S-16** Recover from getting stuck (hybrid escalation) | U-13 | — | Full story shipped. StuckDetector fires, ShiftStateMachine transitions Working → Stuck → Recovering → (Working OR Exiting). Configured thresholds via [requirements.md](../requirements/requirements.md) FR-WORK-13 default values; GMCM exposure of the thresholds in U-17. |
| **S-17** Survive player attacks without abandoning the shift | U-13 | — | Full story shipped. FarmhandNpc overrides damage hooks, returns 0 damage, plays ouch emote, resumes task. |
| **S-18** Multiplayer refuses to load with a friendly message | U-08 | — | Full story shipped. MultiplayerGuard short-circuits BulletinBoardPatch in multiplayer; friendly log message written. |

### Section 5 — Maintainability

| Story | Primary | Completing | State after primary unit |
|---|---|---|---|
| **S-19** Pure logic separable from SMAPI for testability | U-02 | U-04, U-05, U-06, U-10, U-17 | At U-02: `Dayswork.Tests` project compiles against only `Dayswork.Core`; FsCheck + xUnit wired; seed-logging + shrunk-input logging convention established (PBT-08 + PBT-09 obligations satisfied as infrastructure). Each later foundation unit delivers its specific PBT obligation: U-04 ZoneGeometry round-trip + invariants (PBT-02, PBT-03); U-05 rate/deposit/refund invariants (PBT-03); U-06 SaveDataSerializer round-trip (PBT-02 primary); U-10 ShiftStateMachine + ItemBuffer invariants (PBT-02, PBT-03). U-17 ratifies the architectural promise with the i18n lint test surveying the whole assembly. |
| **S-20** Externalize all user-visible strings for community translation | U-08 | U-17 | At U-08: I18nHelper exists, `i18n/default.json` is the source of truth for all U-08-introduced strings; every subsequent UI-introducing unit (U-09, U-11, U-12, U-14, U-15, U-16, U-17) adds its keys to the same file. At U-17: lint test enforces that no user-visible string exists outside `I18nHelper` callsites — proves the architectural promise. |

---

## Coverage verification

| Story | Has at least one delivering unit? |
|---|---|
| S-01 | ✅ U-08 |
| S-02 | ✅ U-09 |
| S-03 | ✅ U-11 (primary), U-16 (selected buildings become executable) |
| S-04 | ✅ U-11 (primary), U-14 (completes fallback), U-16 (building-interior destinations) |
| S-05 | ✅ U-09 (primary), U-12 (completes) |
| S-06 | ✅ U-09 |
| S-07 | ✅ U-10 (primary), U-13 (real walking), U-13B (completes — Farmer + tool-swap) |
| S-08 | ✅ U-10 (primary), U-13 (outdoor worker behavior), U-16 (completes animal/building work) |
| S-09 | ✅ U-10 (primary), U-13 (completes) |
| S-10 | ✅ U-10 (primary), U-14 (completes) |
| S-11 | ✅ U-14 |
| S-12 | ✅ U-12 (primary), U-15 (completes) |
| S-13 | ✅ U-17 |
| S-14 | ✅ U-15 |
| S-15 | ✅ U-15 |
| S-16 | ✅ U-13 |
| S-17 | ✅ U-13 |
| S-18 | ✅ U-08 |
| S-19 | ✅ U-02 (infra), U-04/U-05/U-06/U-10 (specific PBT obligations), U-17 (lint) |
| S-20 | ✅ U-08 (primary), U-17 (lint completes) |

**All 20 stories are covered.** ✓

---

## Stories by unit (inverse map)

For developers picking up a unit, what stories does Construction need to satisfy?

| Unit | Stories delivered (primary or completing) |
|---|---|
| U-01 Project Scaffold | (foundational — no stories directly; sets up the Core/Mod split underpinning S-19) |
| U-02 Test Infrastructure | S-19 (infra portion: PBT-08, PBT-09 obligations) |
| U-03 Config Foundation | (foundation for S-13 — GMCM later exposes these fields) |
| U-04 Geometry & Domain Primitives | S-19 (PBT-02, PBT-03 for ZoneGeometry) |
| U-05 Pricing Core | S-19 (PBT-03 invariants for rate/deposit/refund) |
| U-06 Persistence Core | S-19 (PBT-02 primary obligation), foundation for S-05 |
| U-07 Capability & Priority Core | (foundation for S-08, S-09) |
| U-08 Bulletin Board + i18n + MP Guard | S-01, S-18, S-20 (primary) |
| U-09 Minimum Hiring Flow | S-02, S-06, S-05 (primary) |
| U-10 Minimum Worker Shift | S-07 (primary), S-08 (primary), S-09 (primary), S-10 (primary), S-19 (PBT for state machine + buffer) |
| U-11 Hiring UI: Zones & Chests | S-03, S-04 (primary) |
| U-12 Hiring UI: Schedule + Edit/Pause/Cancel | S-05 (completes), S-12 (primary) |
| U-13 Worker AI: Priority + Capability/Skip + Stuck + Invuln | S-08 (outdoor task ordering/deepening), S-09 (completes), S-16, S-17 |
| U-13B Farmer Worker + Tool Visuals | S-07 (completes) |
| U-14 Output: Multi-Destination Deposit + Overflow Mail | S-04 (completes), S-10 (completes), S-11 |
| U-15 Recurring Lifecycle + Calendar | S-12 (completes), S-14, S-15 |
| U-16 Animals & Buildings | S-08 (completes animal tasks and building-interior work), S-03/S-04 deepening for selected buildings — implemented in U-16 Code Generation 2026-05-22 |
| U-17 GMCM + i18n Polish | S-13, S-19 (lint completes), S-20 (lint completes) |

---

## Cross-cutting concerns

A few requirements groups thread across multiple units rather than concentrating in one. They're listed here so per-unit Construction can pick up the relevant slices:

- **i18n (NFR-UX-02 / FR-CFG-02 / S-20)**: Every UI- or mail-introducing unit must route its user-visible strings through `I18nHelper` (introduced in U-08). The lint test in U-17 enforces this end-to-end.
- **Gamepad navigation (FR-HIRE-03 / NFR-UX-01)**: Every menu-introducing unit (U-08, U-09, U-11, U-12, U-17) must verify gamepad navigation as part of its play-test gate.
- **Multiplayer guard (FR-MP-01 / S-18)**: MultiplayerGuard (introduced in U-08) must be consulted in every entry-point unit. The three callsites per [components.md](components.md) M-18 are: M-01 ModEntry (short-circuit in U-08 itself), M-02 BulletinBoardPatch (U-08), M-13 RecurringContractScheduler (skeleton in U-10, full check in U-15).
- **No items lost (NFR-SAFE-01)**: This invariant cuts across U-10 (single-trip deposit case) and U-14 (multi-trip + overflow mail case). U-14's Construction loop must verify NFR-SAFE-01 still holds in every overflow / missing-chest / destroyed-chest path.
- **Integer rounding (NFR-SAFE-02)**: Originates in U-05's PBT-03 invariants (`deposit − refund == hoursWorked × rate` modulo integer rounding). Must continue to hold as RefundCalculator is called from U-10's exit path and any future deepening that touches the math.
- **Tolerate absent save data (NFR-SAFE-03)**: U-06 owns this for the contracts segment. U-09 wires the persistence adapter that exposes the behavior to the player.
- **Harmony isolation (NFR-MAINT-04)**: U-08 establishes `Dayswork/Patches/` namespace. Every subsequent unit that adds a Harmony patch (none anticipated in this plan — only U-08 needs one) must use this namespace.
