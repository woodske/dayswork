# U-22 — Scope-Driven Runtime Alignment: Frontend Components

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A (authoritative typed scope only), FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X (no older contracts to support), FD-Q9=A

U-22 is primarily a runtime unit, but the approved answers do require a small UI wording pass so the scope screens still truthfully describe how the worker behaves.

---

## Affected menus

| Menu / projection seam | U-22 responsibility |
|---|---|
| `ZoneAndChestMenu` | Clarify building-owned animal scope and dedicated greenhouse scope in the scope summary area. |
| `SummaryMenu` or shared validation-message text | Ensure any scope-facing wording remains consistent with the dedicated greenhouse / building-owned animal model if that text is surfaced there. |
| `OutputDestinationsMenu` | No structural change required; destinations remain task-owned. |

---

## Scope-page wording updates

### Animal buildings section

The current separate "Animal buildings" section remains the right shape. U-22 only needs the wording to make the runtime rule explicit.

The section should communicate:
- selected barns/coops define *which animals belong to the contract*
- those animals are serviced wherever they currently are on the farm

Examples of the kind of copy this section should support:
- summary label: `Animal buildings`
- summary value: selected building names as it does today
- helper/microcopy: `Selected barns and coops cover their assigned animals wherever they are on the farm.`

### Greenhouse section

The greenhouse section should continue to be separate from animal buildings and outdoor work areas.

The section should communicate:
- greenhouse selection means a dedicated crop work area
- it is not just another generic building in the scope list

Examples of the kind of copy this section should support:
- summary label: `Greenhouse`
- summary value: selected / not selected
- helper/microcopy: `The greenhouse runs as its own crop work area.`

---

## Validation and summary text alignment

If the scope page or summary page surfaces validation guidance, that wording should align with the runtime rules:
- missing selected-animal scope should point the player toward choosing barns/coops
- missing greenhouse scope should point the player toward choosing the greenhouse for greenhouse crop services

The approved design does **not** require:
- a new page
- a new selector control
- new per-scope destination controls

It only requires the wording to stay faithful to the runtime semantics.

---

## Non-goals for this unit

U-22 does not reopen the larger U-20 UI architecture work. It does not add:
- per-building destination controls
- scope-family destination controls
- a second review screen
- a new visualization for overflow mail categories

Scope-aware overflow categories are a runtime/mail concern in this unit, not a new front-end workflow.
