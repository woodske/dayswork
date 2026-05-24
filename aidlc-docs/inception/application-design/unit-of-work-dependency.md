# Unit of Work Dependency — Pricing Model Redesign Retrofit

This document defines the execution order for the appended retrofit units `U-18` through `U-24`.

Historical units `U-01` through `U-17` are treated as the already-built baseline. The retrofit sequence starts only after that historical baseline.

---

## Dependency DAG (Mermaid)

```mermaid
flowchart TD
    HB["Historical Baseline<br/><b>U-01 .. U-17</b>"]
    U18["U-18 Contract Terms Foundation"]
    U19["U-19 Contract Snapshot Persistence + Legacy Cleanup"]
    U20["U-20 Hiring Flow Preview Refresh"]
    U21["U-21 Worker Energy + Shift Runtime Refresh"]
    U22["U-22 Scope-Driven Runtime Alignment"]
    U23["U-23 Recurring Billing + Calendar Refresh"]
    U24["U-24 Config, Regression, and Documentation Cleanup"]

    HB --> U18
    U18 --> U19
    U19 --> U20
    U19 --> U21
    U20 --> U22
    U21 --> U22
    U20 --> U23
    U21 --> U23
    U22 --> U23
    U23 --> U24

    classDef baseline fill:#BDBDBD,stroke:#424242,stroke-width:2px,color:#000
    classDef foundation fill:#C8E6C9,stroke:#2E7D32,stroke-width:2px,color:#000
    classDef feature fill:#BBDEFB,stroke:#1565C0,stroke-width:2px,color:#000
    classDef cleanup fill:#FFE082,stroke:#F57F17,stroke-width:2px,color:#000

    class HB baseline
    class U18,U19 foundation
    class U20,U21,U22,U23 feature
    class U24 cleanup
```

**Color legend**:
- gray = historical baseline
- green = retrofit foundation
- blue = feature/runtime retrofit slices
- amber = final cleanup/regression unit

---

## Text fallback — dependency summary

| Unit | Depends on | Why |
|---|---|---|
| `U-18` Contract Terms Foundation | Historical baseline | Replaces the pricing architecture on top of the already-built mod |
| `U-19` Contract Snapshot Persistence + Legacy Cleanup | `U-18` | Persistence schema must follow the new contract-terms model |
| `U-20` Hiring Flow Preview Refresh | `U-19` | The UI should preview and confirm the persisted contract-terms shape |
| `U-21` Worker Energy + Shift Runtime Refresh | `U-19` | Runtime needs the new stored terms/energy model available on contracts |
| `U-22` Scope-Driven Runtime Alignment | `U-20`, `U-21` | Runtime scope consumption must agree with both the new UI selection model and the new energy-aware shift runtime |
| `U-23` Recurring Billing + Calendar Refresh | `U-20`, `U-21`, `U-22` | Day-start repricing and calendar rules need the final hire-flow terms shape plus runtime scope behavior |
| `U-24` Config, Regression, and Documentation Cleanup | `U-23` | Final cleanup should happen after all behavior changes settle |

---

## Recommended execution order

### Historical prerequisite
1. `U-01 .. U-17` historical baseline already exists

### Retrofit sequence
2. `U-18` Contract Terms Foundation
3. `U-19` Contract Snapshot Persistence + Legacy Cleanup
4. `U-20` Hiring Flow Preview Refresh
5. `U-21` Worker Energy + Shift Runtime Refresh
6. `U-22` Scope-Driven Runtime Alignment
7. `U-23` Recurring Billing + Calendar Refresh
8. `U-24` Config, Regression, and Documentation Cleanup

This sequence is intentionally more linear than the raw DAG because:
- it reduces context switching for a solo developer
- it lets the contract model settle before the UI and runtime both start changing
- it postpones regression/doc cleanup until behavior is stable enough to document honestly

---

## Construction loop handoff

Each retrofit unit completes the normal Construction loop before the next begins:

1. Functional Design
2. NFR Requirements
3. NFR Design
4. Infrastructure Design skipped
5. Code Generation

Only after `U-24` is approved does the redesign move to the global Build and Test stage.

---

## Risk notes by dependency edge

| Edge | Risk being controlled |
|---|---|
| `U-18 -> U-19` | Prevents persistence from guessing at an unstable contract-terms shape |
| `U-19 -> U-20` | Prevents the UI from previewing a model that cannot yet be saved correctly |
| `U-19 -> U-21` | Prevents the runtime from depending on contract data that is not yet persisted coherently |
| `U-20/U-21 -> U-22` | Prevents runtime scope consumption from drifting away from what the player selected in the refreshed UI |
| `U-20/U-21/U-22 -> U-23` | Prevents recurring lifecycle work from being built on partially updated pricing/runtime semantics |
| `U-23 -> U-24` | Prevents docs/config/regression from freezing too early while recurring behavior is still moving |

---

## Validation

- No retrofit unit depends on a later retrofit unit
- Every retrofit unit has a clear predecessor chain back to the historical baseline
- The sequence preserves the approved hybrid strategy: foundation first, then feature/runtime slices, then final cleanup
