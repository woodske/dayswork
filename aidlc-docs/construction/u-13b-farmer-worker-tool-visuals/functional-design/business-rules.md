# U-13B — Business Rules

**Unit**: U-13B — Farmer Worker + Tool Visuals

Rules use U-13B-scoped IDs. U-13B authoritatively defines the Farmer/visual rules that were drafted under U-13 and deferred at the 2026-05-21 split (the U-13 artifacts banner these as "→ U-13B"). Each rule cites its source requirement and the answered design question. All U-13 behavioural rules (priority, capability/skip, stuck, state machine) remain in force unchanged and are **not** restated here.

---

## Worker entity rules (FD-Q5=B)

**BR-WORKER-01** — The worker is a `Farmer` with a randomized character-creation appearance, created at 6am and removed at shift end. It is **never** written to the save file and **never** added to `location.characters` or `location.farmers`; it is held only by the mod's own reference. On save-during-shift it is removed and the deposit refunded (U-10/U-13 `OnSaving` pattern retained verbatim). *(FD-Q5=B; save-integrity)*

**BR-WORKER-02** — Worker movement is driven by a custom path-follower: a route is computed via the game's A* (a throwaway `PathFindController` read for its `pathToEndPoint`, then discarded) and the worker is stepped manually each tick (set `Farmer.Position`, set facing, advance the walk animation). `PathFindController` is **never** ticked to drive the worker. Arrived / no-path signals match the prior adapter surface (`HasArrived` / `NavigationFailed`). *(FD-Q5=B; FD-Q1=A)*

**BR-WORKER-03** — The worker is drawn each frame via a `Display.RenderedWorld` hook at its correct screen position, internally Y-ordered so its own parts (body, held tool, shadow, emote) layer correctly. **Accepted v1 limitation:** because the hook fires after the world draw pass, the worker may render *over* foreground objects (tree canopies, building edges) it is standing behind. This is a cosmetic-only concession; true world-interleaved occlusion (a Harmony draw-pass injection) is deferred. Logged as a play-test note. *(FD-Q5=B; FD-Q2=A)*

**BR-WORKER-04** — The worker carries the captured `ToolSnapshot` so its real tools match the player's upgrade levels at spawn. *(FR-TOOL-01)*

**BR-WORKER-05** — Worker movement speed equals the vanilla player base walk speed (no running). This keeps the worker's pace natural and consistent with the stuck-detection thresholds tuned in U-13. *(FR-WORK-02; FD-Q1=A consistency)*

---

## Appearance rules (FD-Q3=A, FD-Q4=A)

**BR-APPEAR-01** — The worker's appearance is generated **deterministically from the contract ID**: the same contract yields the same appearance every day (stable across recurring runs) with no serialization; different contracts yield different-looking workers. *(FD-Q3=A)*

**BR-APPEAR-02** — Appearance randomization covers the full character-creation field set: gender/body, skin tone, hair style + colour, shirt, pants + colour, accessory, eye colour. All indices are clamped to valid character-creation ranges so no invalid sprite index is ever produced. *(FD-Q4=A)*

**BR-APPEAR-03** — Appearance is purely cosmetic and never influences work behaviour, capability, pricing, or timing. *(FD-Q4=A; separation of concerns)*

---

## Tool-swap visual rules (FR-WORK-10; S-07)

**BR-VIS-01** — Tool→task mapping (the Core `WorkerTool` map): Water crops = Watering Can; Clear weeds & Clear grass = Scythe; Clear rocks = Pickaxe; Cut trees = Axe; Harvest crops & Collect fruit = no tool. *(FR-WORK-10)*

**BR-VIS-02** — A visible one-shot swing (`FarmerSprite.animateOnce` with the verified per-direction frame set: heavy R12/R9/R7, watering can R10/R5/R8/R11, scythe R5/R6/R7) plays for each tool-using task action, synchronized with the existing Invoke-and-Poll task completion. The worker faces the task tile before the swing. *(FR-WORK-10; S-07)*

**BR-VIS-03** — The held tool is the worker `Farmer`'s real current tool, drawn by `FarmerRenderer`; no overlay icon is used. *(FD-Q5=B)*

**BR-VIS-04** — Tool swaps on a task-class change are **instant**: the new tool appears on the next swing with no equip delay. There is therefore no swap interval for the stuck detector / 8pm cap to special-case. *(FD-Q6=A)*

**BR-VIS-05** — No-tool tasks (Harvest crops, Collect fruit) play **no swing and draw no tool**; instead the worker faces the target tile and pauses briefly (hand-pick beat) while the action resolves. *(FD-Q5=A; FR-WORK-10)*

---

## Invulnerability rules (carried unchanged from U-13)

**BR-INVULN-01** — The worker takes no damage from player weapons: a `Farmer` has no single-player friendly-fire path, so the worker is inherently invulnerable (no damage override needed). *(FR-NPC-02; S-17)*

**BR-INVULN-02** — When the player swings a melee weapon within melee range of the worker, the worker plays a "!" surprised emote once per swing and does not interrupt its current intent or change shift state. The emote bubble is drawn by `WorkerRenderer` since the worker is outside the game's character-draw pass. *(FR-NPC-02; FD-Q6=A from U-13)*

---

## Behaviour-preservation rule (regression guard)

**BR-PRESERVE-01** — U-13B must preserve all U-13 worker behaviour: priority-grouped work list, capability/skip rules (BR-SKIP-01..06, BR-TOOL-01/02), trellis adjacency, stuck detection + 3-step escalation (BR-STUCK-01..05), the state-machine phases/transitions (BR-SM-01..03), deposit/exit, and refund math. U-13B changes only the entity, movement, rendering, appearance, and tool-visual seams; any change to behavioural logic is out of scope and a defect. The U-13 play-test "worker stands still" symptom must be resolved as a consequence of BR-WORKER-02 (manual per-tick stepping), and reliable arrival at each work tile is an explicit Definition-of-Done item. *(U-13/U-13B split intent; U-13 play-test follow-up)*

---

## Requirements deviation (re-affirmed; already recorded as DEV-01)

**DEV-01 — FR-NPC-01 (placeholder sprite).** FR-NPC-01 specified a recolored vanilla **NPC** placeholder sprite for v1. U-13B realizes the superseding decision (FD-Q5=B): a randomized **Farmer** appearance, chosen to deliver authentic player-style tool animations (FR-WORK-10 / S-07) and to align with the Farmer-native post-V1 roadmap. Custom NPC art remains out of scope. Already logged in `aidlc-state.md`; to be reflected in the requirements record at the next requirements touch-point.
