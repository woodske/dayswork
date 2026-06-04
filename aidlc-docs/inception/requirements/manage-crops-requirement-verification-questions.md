# Manage Crops — Requirements Clarification Questions

The feature spec at [`aidlc-docs/inception/manage-crops-spec.md`](../manage-crops-spec.md)
is unusually complete: all eight original open questions (OQ-1..OQ-8) are resolved
and a full decisions log is recorded in §10–§11. These clarifying questions
therefore cover only the genuine **requirements-level** decisions the spec leaves
open, plus the two mandatory extension opt-ins.

Please answer each question by filling in the letter after the `[Answer]:` tag. If
none of the options fit, choose the last option (Other) and describe your choice.

---

## Question 1 — Delivery scope of this change
The spec is large (new hub page + crop-first authoring, viability-gated planting,
self-healing field maintenance, autonomous town shopping with headless 1.6 shop
transactions + new town-store navigation, two cabin chests, per-zone output
routing, greenhouse/shed support, save schema V2→V3). How should this be delivered?

A) **Full spec in one feature** — decompose into multiple sequential units of work
   (the normal AI-DLC unit loop) but treat the entire spec as in-scope for this
   change, delivered unit by unit until complete. (Recommended)
B) **Phased subset first** — implement a reduced first iteration now (e.g. authoring
   UI + data model + field maintenance/planting from the input chest, **deferring**
   autonomous town shopping/navigation to a later change) and track the rest as TODOs.
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2 — "Preferred store closed → using fallback" notification
§7 lists this as a **(Candidate)** HUD notification (the other notifications are
confirmed). Should it be in scope as a required notification for this change?

A) **Yes** — emit a HUD notice when the preferred store is closed and the farmhand
   falls back to the other store. (Recommended)
B) No — silently use the fallback store; do not add this notification in this change.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3 — Input-chest backfill for existing saved offices
§8.3 flags a ⚠ watch item: offices placed **before** this update have only the
output chest, and a newly-declared second `BuildingChest` may not be auto-created on
already-built instances. What is the requirement for existing saves?

A) **Required** — guarantee the new input chest exists for pre-existing
   `Bindicle.Dayswork_Office` buildings on load (add a one-time backfill if the
   game does not auto-create it), so the feature works on saves that already have an
   office. (Recommended)
B) Best-effort — rely on whatever the game auto-creates; if the input chest is
   missing on old saves, the player rebuilds the office (no dedicated backfill).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4 — Default state of the two global Manage-Crops toggles
§6.5 / §6.6 add two global toggles — "clear debris before tilling" and "clear dead
plants" — but do not pin their **default** values. §6.6 notes auto-replant relies on
dead-plant clearing being enabled. What should the out-of-the-box defaults be?

A) **Both ON by default** — debris clearing and dead-plant clearing both enabled, so
   self-healing/replanting works without extra setup. (Recommended)
B) Both OFF by default — opt-in; the player must enable each toggle explicitly.
C) Clear debris ON, clear dead plants OFF (debris is cheap; dead-plant clearing is
   opt-in).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 5 — Security Extensions (mandatory opt-in)
Should security extension rules be enforced for this project?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended for
   production-grade applications)
B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental
   projects). *(This matches the standing project decision: no network/PII/auth
   surface — a local single-player SMAPI mod.)*
X) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question 6 — Property-Based Testing Extension (mandatory opt-in)
Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects
   with business logic, data transformations, serialization, or stateful
   components). *(This matches the standing project decision — FsCheck full mode —
   and this feature has rich pure logic: viability math, supply/min(seeds,fert)
   planning, season assignment, save round-trips.)*
B) Partial — enforce PBT rules only for pure functions and serialization round-trips
C) No — skip all PBT rules
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

When you're done, let me know (e.g. "done") and I'll validate the answers, check for
contradictions, and generate the requirements document.
