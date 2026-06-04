# Unit of Work Plan — Manage Crops

**Stage**: Units Generation — Part 1 (Planning)
**Source**: [manage-crops-application-design.md](../application-design/manage-crops-application-design.md) (C-24..C-31, M-24..M-29); requirements FR-MC-01..44 / NFR-MC-01..09; stories S-27..S-35.
**Naming**: continues the project's appended-unit convention (U-01..U-17 historical, U-18..U-25 retrofit, U-SVE-01..04, U-T09/U-T10). Manage Crops units will be **U-MC-01..U-MC-07**.

## Proposed decomposition (foundation-first, hybrid slicing)
| Unit | Title | Owns (design refs) | Stories |
|---|---|---|---|
| **U-MC-01** | Crop-plan domain + persistence foundation | C-24, C-25, C-26, C-27, C-28, C-29 (skeleton), C-30, C-31; V2→V3 migration | S-34, S-35 |
| **U-MC-02** | Cabin chests (input + backfill) | M-28; `HiringBuilding`/`ChestResolver` ext | S-31 (chests), S-34 |
| **U-MC-03** | Manage Crops authoring UI | M-24, M-25, C-27 (wiring); `HubMenu`/`ContractDraft` ext | S-27 |
| **U-MC-04** | Zone draw overlay extension | `ZoneDrawOverlay`/`ZoneDrawMenu` ext (DEV-MC-01) | S-28 |
| **U-MC-05** | Shift crop behavior | M-27 (core), C-29; `ShiftPlanBuilder`/`ShiftOrchestrator`/`WorkerTool`/`CapabilityEvaluator`/`WorkActionKind` ext | S-29, S-33 |
| **U-MC-06** | Town shopping | M-26, C-26/C-28 (wiring), M-29; `CrossLocationRouteNavigator` ext | S-30 |
| **U-MC-07** | Output routing + greenhouse/shed | M-27 (routing), `SveExpansionProfile` ext | S-31 (routing), S-32 |

Per-unit example + FsCheck coverage (full-mode PBT) lands with each unit; final regression consolidated in Build and Test.

---

## Planning Questions

Please answer each by filling in the letter after the `[Answer]:` tag.

### Question 1 — Unit count & granularity
Confirm the decomposition granularity.

A) **~7 medium units (U-MC-01..07)** as proposed above — foundation-first, each independently buildable/testable. (Recommended)
B) Fewer, larger units (~3–4).
C) More, finer units (~10+).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Slicing strategy
How should units be sliced?

A) **Foundation-first hybrid** — domain/persistence first (U-MC-01), then chests/UI/overlay, then runtime shift behavior, then shopping, then output/greenhouse — matching the project's existing retrofit/SVE approach. (Recommended)
B) Vertical feature slices (each unit a thin end-to-end slice of authoring→runtime).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3 — Final cleanup/regression unit
Should there be a distinct final cleanup/regression unit (as with U-24/U-25), or fold final regression into Build and Test?

A) **Fold final regression into Build and Test** — each unit carries its own tests; the global Build and Test stage consolidates regression + manual playtest. No separate cleanup unit. (Recommended)
B) Add a distinct final cleanup/regression unit (U-MC-08).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Sequencing / dependencies
Confirm the build order.

A) **Sequential U-MC-01 → 07** in the order above (each depends only on earlier units; U-MC-01 is the shared foundation). (Recommended)
B) A different order (please describe after [Answer]: tag).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Mandatory Unit Artifacts (Part 2 — Generation) — COMPLETE
- [x] Append U-MC-01..07 entries to `unit-of-work.md` (definitions, owns/extends, stories).
- [x] Append Manage Crops rows to `unit-of-work-dependency.md` (dependency matrix).
- [x] Append Manage Crops mappings to `unit-of-work-story-map.md` (S-27..S-35 → units).
- [x] Validate unit boundaries; every Manage Crops story (S-27..S-35) is assigned; no unit depends on a later unit.

---

When you've answered the questions, let me know (e.g. "done"). I'll check for ambiguity/contradiction, and once the plan is approved, generate the unit artifacts.
