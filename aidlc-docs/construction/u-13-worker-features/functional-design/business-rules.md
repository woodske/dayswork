# U-13 — Business Rules

> **SCOPE SPLIT (2026-05-21):** The Farmer/visual rules — **BR-VIS-01..03** (tool-swap), **BR-WORKER-01..04** (Farmer worker, manual movement, depth-sorted draw), and **DEV-01** (FR-NPC-01 → Farmer) — are **deferred to U-13B**. U-13 keeps the `NPC` worker (with real `PathFindController` walking) and enforces the priority/skip/capability/stuck/state-machine/invulnerability rules below. *Exception:* **BR-INVULN-01/02** still apply in U-13 — a plain villager `NPC` is also undamageable by the player, so invulnerability + the swing-detected "!" emote ship in U-13.

**Unit**: U-13 — Worker AI: Priority + Capability/Skip + Stuck + Invulnerability *(Farmer/visual rules → U-13B)*

Rules use U-13-scoped IDs to avoid collision with the global BR sequence. Each rule cites its source requirement.

---

## Priority & work-list rules

**BR-PRIO-01** — Within a shift, work items execute grouped by `TaskKind` in the fixed FR-WORK-03 order (Feed → Pet → Collect animal products → Water → Harvest → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees), with tiles ordered nearest-first inside each group. *(FR-WORK-03; FD-Q2-A from U-10 carried forward)*

**BR-PRIO-02** — The priority orderer is fed every enabled task kind, but in U-13 only the 7 outdoor kinds produce work items. The 3 animal kinds occupy their priority slots and contribute zero items. *(FD-Q1=A)*

**BR-PRIO-03** — U-13 does not scan building interiors (including the greenhouse). The U-10 building pre-pass is removed. *(FD-Q1=A; deferred — TODO-05)*

---

## Capability & skip rules

**BR-SKIP-01** — A chop target the snapshot axe level cannot process is silently skipped: `LargeStump` requires Steel+, `LargeLog` requires Gold+. *(FR-SKIP-01)*

**BR-SKIP-02** — A break target the snapshot pickaxe level cannot process is silently skipped: `LargeBoulder` requires Steel+, `Meteorite` requires Gold+. *(FR-SKIP-02)*

**BR-SKIP-03** — Fruit trees are never felled regardless of axe level or zone. *(FR-SKIP-03; already enforced by `CapabilityMatrix`)*

**BR-SKIP-04** — A trellis crop is harvested from the **nearest reachable orthogonal neighbor** of the worker's current position. If no orthogonal neighbor is reachable, the crop is skipped. *(FR-SKIP-04; FD-Q4=B)*

**BR-SKIP-05** — Crops not yet ready to harvest produce no Harvest work item. *(FR-SKIP-05)*

**BR-SKIP-06** — Skipped tiles do not count toward estimated or actual hours worked. *(FR-WORK-08 consistency)*

**BR-TOOL-01** — Capability is evaluated against the `ToolSnapshot` captured at 6am and is fixed for the shift; mid-day tool changes do not affect the worker. *(FR-TOOL-01)*

**BR-TOOL-02** — If an entire enabled task type is skipped solely because the player lacks the required tool, the task kind is recorded in `ShiftContext.ToolMissingWarnings`. U-13 only records it; the warning mail is delivered by U-14. *(S-09; mail deferred to U-14)*

---

## Tool-swap visual rules

**BR-VIS-01** — Tool→task mapping: Water = Watering Can; Clear weeds & Clear grass = Scythe; Clear rocks = Pickaxe; Cut trees = Axe; Harvest crops & Collect fruit = no tool. *(FR-WORK-10)*

**BR-VIS-02** — A visible tool swing plays for each tool-using task action, synchronized with the existing Invoke-and-Poll task completion. Tasks mapped to "no tool" play no swing. *(FR-WORK-10; S-07)*

**BR-VIS-03** — The held tool is the worker `Farmer`'s real `CurrentTool`, drawn by `FarmerRenderer`; no overlay icon is used. *(FD-Q5=B)*

---

## Stuck detection & escalation rules

**BR-STUCK-01** — "Progress this tick" = the worker's tile coordinate changed during navigation, OR the worker is performing a task action (task actions always count as progress). *(FR-WORK-11; FD-Q3=A)*

**BR-STUCK-02** — Stuck fires when the no-progress accumulator reaches the active threshold: `StuckInitialThresholdMinutes` (default 10) before the first teleport, `StuckPostTeleportThresholdMinutes` (default 10) after. *(FR-WORK-11/13)*

**BR-STUCK-03** — Escalation is exactly three steps: (1) confused "?" emote, (2) teleport to the next reachable task tile and resume work, (3) on a second stuck window or when no reachable tile exists, teleport to the farm entrance and end the shift early as if the 8pm cap fired. *(FR-WORK-12)*

**BR-STUCK-04** — The escalation counter (`RecoveryAttempts`) is held by the orchestrator/shift context, not the state machine. The detector is `Reset()` on every teleport and on every progress tick. *(FD-Q2=A)*

**BR-STUCK-05** — On an early end (step 3), buffered items are deposited at the shipping bin (always reachable in U-13) and the refund is computed from actual hours worked using the early end time, identical to the 8pm-cap path. *(FR-WORK-12; FR-PAY-05; multi-chest/mail deferred to U-14)*

---

## State machine rules

**BR-SM-01** — Legal transitions: `WaitingForSpawn→Working`; `Working→{Depositing, Stuck}`; `Stuck→Recovering`; `Recovering→{Working, Depositing}`; `Depositing→Exiting`; `Exiting→Done`. Any other transition throws. *(FD-Q2=A)*

**BR-SM-02** — `Done` is terminal; no transition out of `Done` is ever valid. *(carried from U-10; PBT invariant)*

**BR-SM-03** — Active phases (`Working`, `Stuck`, `Recovering`, `Depositing`, `Exiting`) must carry a non-null intent; inactive phases (`WaitingForSpawn`, `Done`) must not. *(extends U-10 invariant)*

---

## Worker entity rules (FD-Q5=B)

**BR-WORKER-01** — The worker is a `Farmer` with randomized character-creation appearance, created at 6am and removed at shift end. It is **never** written to the save file and **never** added to `location.characters` or `location.farmers`; on save-during-shift it is removed and the full deposit refunded (U-10 pattern retained). *(FD-Q5=B; NFR-SAFE relating to save integrity)*

**BR-WORKER-02** — Worker movement is driven by a custom path-follower (compute tile path, step `Farmer.Position` + advance walk animation per tick), not `PathFindController`. Arrived / no-path signals match the U-10 adapter surface. *(FD-Q5=B)*

**BR-WORKER-03** — The worker is drawn each frame ordered by world Y for depth sorting. *Accepted v1 fallback:* if exact depth parity is impractical, the worker may draw above foreground objects; this is a cosmetic-only concession and must be logged as a play-test TODO if taken. *(FD-Q5=B)*

**BR-WORKER-04** — The worker carries the captured `ToolSnapshot` so its real tools match the player's upgrade levels at spawn. *(FR-TOOL-01)*

---

## Invulnerability rules

**BR-INVULN-01** — The worker takes no damage from player weapons (a `Farmer` has no single-player friendly-fire path; the worker is inherently invulnerable). *(FR-NPC-02; S-17)*

**BR-INVULN-02** — When the player swings a melee weapon within melee range of the worker, the worker plays a "!" surprised emote once per swing and does not interrupt its current intent or change shift state. *(FR-NPC-02; FD-Q6=A)*

---

## Requirements deviation recorded by this unit

**DEV-01 — FR-NPC-01 (placeholder sprite).** FR-NPC-01 specified a recolored vanilla **NPC** placeholder sprite for v1. U-13 supersedes this with a randomized **Farmer** appearance (FD-Q5=B), chosen to deliver authentic player-style tool animations (FR-WORK-10 / S-07) and to align with the post-V1 roadmap (energy bar, worker-owned tools, food/buffs are Farmer-native). Custom NPC art remains out of scope. This deviation should be reflected in the requirements record at the next requirements touch-point.
