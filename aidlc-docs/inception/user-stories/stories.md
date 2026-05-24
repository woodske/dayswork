# User Stories — Dayswork

**Personas**: see [personas.md](personas.md). P-01 = Player, P-02 = Farmhand, P-03 = Mod Maintainer.

**Organization**: Stories are grouped by **user journey** — the natural flow of a player encountering and using the mod over time. Within each section, stories are ordered as they would unfold in real play.

**Story format**: `As [persona], I want [capability], so that [benefit]`.

**Acceptance criteria format**: **Gherkin** (Given/When/Then) for behaviors involving state transitions; **bullets** for UI/visual rules and simple presence checks.

**Traceability**: Each story lists the FR-IDs from [requirements.md](../requirements/requirements.md) that it implements. All v1 FRs are covered across the story set; no prioritization markers per Q8 of the planning document.

---

## Section 1 — Discovery & First Hire

The player encounters the new feature on the bulletin board and walks through the four-screen hiring UI for their first contract.

### S-01 — Discover the hiring option on the bulletin board

**As** P-01 the Player,
**I want** to see a "Hire a Farmhand" option when I open the Pelican Town bulletin board,
**so that** I can discover this feature without reading docs and without a new building cluttering the map.

**Implements**: FR-HIRE-01

**Acceptance criteria (UI/visual — bullets):**
- The vanilla bulletin board menu opens unchanged when the player interacts with it.
- A new "Hire a Farmhand" entry is visible alongside the existing quest entries.
- The entry uses the same visual style (font, padding, icon convention) as the vanilla entries.
- Clicking / activating the entry opens the mod's custom hiring UI (Screen 1) without first closing the bulletin board jankily.
- The entry is hidden when the mod is loaded in a multiplayer session (per FR-MP-01).

---

### S-02 — Configure tasks and see the live contract price

**As** P-01 the Player,
**I want** to toggle which tasks the farmhand will perform and see the contract price change in real time,
**so that** I can shape the contract to my farm's needs and understand exactly what I'm being charged.

**Implements**: FR-HIRE-04, FR-PAY-01, FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-PAY-11, NFR-UX-04

**Acceptance criteria (UI/visual — bullets):**
- Screen 1 lists all ten v1 tasks (FR-TASK-01) as toggles.
- Toggling a task on/off updates the displayed contract-price preview within the same frame.
- Each selected service shows a visible price contribution or package contribution in the preview.
- Outdoor crop and clearing services communicate that scope bands are derived from the selected zone, not chosen manually by the player.
- Animal-care services communicate that selected barns/coops add building-based pricing.
- Greenhouse crop work is shown as a fixed greenhouse package rather than as a tile-derived outdoor band.
- Price values come from `config.json` / GMCM, not hardcoded.
- The screen is fully navigable with both mouse/keyboard and gamepad (FR-HIRE-03 / NFR-UX-01).

---

### S-03 — Draw zones and select buildings on the farm

**As** P-01 the Player,
**I want** to draw one or more rectangular zones on the farm and/or pick whole buildings to be the work area,
**so that** the farmhand only touches the tiles I authorize.

**Implements**: FR-HIRE-05, FR-TASK-10, FR-TASK-11, FR-TASK-12, FR-WORK-08

**Acceptance criteria (UI/visual — bullets):**
- From Screen 2, choosing "Draw a zone" hides the menu and overlays the farm map.
- Click-and-drag draws a rectangle highlight; releasing the mouse finalizes the rectangle and returns to Screen 2.
- A single contract can hold multiple rectangles and multiple selected buildings.
- Clicking a building (barn, coop, shed, greenhouse, etc.) toggles its selection.
- Barns and coops act as animal-service scope selectors, not just as geometry inside a drawn zone.
- The greenhouse is treated as a special crop-work scope rather than as a normal animal building.
- Drawn zones may overlap unreachable tiles (water, cliffs, walls); these tiles are silently skipped at execution time (FR-WORK-08) — no warning in the UI.
- Gamepad users can move a cursor with the left stick and tap A to anchor / release the rectangle corners.

---

### S-04 — Assign output destinations per task

**As** P-01 the Player,
**I want** to assign an output destination (shipping bin or a specific chest) for each task that produces items,
**so that** my farmhand drops things where I expect them and doesn't fill my one good chest with rocks.

**Implements**: FR-HIRE-06, FR-HIRE-07, FR-HIRE-08, FR-HIRE-09, FR-HIRE-10, FR-TASK-02, FR-TASK-04, FR-OUT-07

**Acceptance criteria (UI/visual — bullets):**
- Each task that produces output (Harvest crops, Collect fruit, Collect animal products, Cut trees, Clear rocks, Clear weeds) shows a "Set output" button on Screen 2.
- For Harvest crops, Collect fruit, and Collect animal products, the player picks **Shipping bin** OR **Chest** (per FR-TASK-02).
- "Chest" assignment uses the split model: open-farm chests are picked by clicking the chest on the farm map (same interaction as zone tile selection); building-interior chests are picked from a dropdown panel grouped by building.
- The dropdown shows each chest's in-game name; if unnamed, the label falls back to `"{Building name} — Chest at {x}, {y}"`.
- Buildings with no chests are omitted from the dropdown entirely.
- Multiple tasks may target the same chest.
- A task with output but no assigned destination is allowed; output buffers and mails the next morning (FR-HIRE-10) with no penalty.

**Acceptance criteria (state — Gherkin):**
- **Given** the player has assigned chest C1 to "Clear rocks"
  **When** the player later renames C1 in-game
  **Then** the assignment still points at C1 (assignments key on location + tile, not name).
- **Given** the player has assigned chest C1 to "Clear rocks"
  **When** the player moves C1 to a different tile
  **Then** the assignment is effectively orphaned and falls back to FR-OUT-04 (buffer + mail).
- **Given** the player selected a barn or coop for animal care
  **When** the worker starts that contract day
  **Then** the worker services animals assigned to that building wherever those animals currently are on the farm, not only inside a drawn rectangle.

---

### S-05 — Choose a one-time or recurring schedule

**As** P-01 the Player,
**I want** to choose between hiring the farmhand for one day or every day,
**so that** I can experiment cheaply or set up a stable daily routine.

**Implements**: FR-HIRE-11, FR-HIRE-12, FR-PERSIST-01

**Acceptance criteria (UI/visual — bullets):**
- Screen 3 presents two options: **One-time** (next morning only) and **Recurring** (every morning automatically).
- Recurring contracts can be paused or cancelled from the bulletin board any time before 6am (visible as an action on the contract entry).
- Recurring contracts show a stable fixed daily price for the saved contract rather than a day-by-day recalculation from actual available work.
- Zone and task settings persist between days and are editable on the bulletin board (FR-HIRE-12).

**Acceptance criteria (state — Gherkin):**
- **Given** the player saves and reloads the game
  **When** the bulletin board is opened
  **Then** any in-flight contracts (one-time scheduled or recurring) are still listed with their original configuration (FR-PERSIST-01).

---

### S-06 — Review the contract, price, and worker stamina before confirming

**As** P-01 the Player,
**I want** a clear summary of everything I'm about to commit to and an obvious confirm step,
**so that** I'm not surprised by the price or by what the farmhand will (or won't) be able to finish.

**Implements**: FR-HIRE-13, FR-HIRE-14, FR-PAY-01, FR-PAY-02, FR-PAY-06, FR-PAY-07, NFR-UX-04

**Acceptance criteria (UI/visual — bullets):**
- Screen 4 shows: selected tasks, selected zones/buildings, pricing breakdown, and a worker energy summary.
- The pricing breakdown makes it clear which charges come from outdoor service scope, animal buildings, and greenhouse package selection.
- The screen explains that the worker continues until the job is done, the day ends, or the worker's energy is exhausted.
- The screen does not present deposit/refund language or estimated-hours math.
- A clear "Confirm" action is present and is the only path that deducts gold.

**Acceptance criteria (state — Gherkin):**
- **Given** the player has enough gold for the one-time contract price
  **When** they press Confirm
  **Then** the contract price is deducted immediately from player gold, the contract is persisted, and the UI closes.
- **Given** the player does *not* have enough gold for the one-time contract price
  **When** they press Confirm
  **Then** confirmation is blocked, no gold is deducted, and a clear error message is shown (FR-HIRE-14).

---

## Section 2 — First Day of Work

The morning after a successful hire, the farmhand executes the contract.

### S-07 — Watch the farmhand arrive and work on day one

**As** P-01 the Player,
**I want** to see the farmhand physically arrive at the farm entrance at 6am and walk to the work area,
**so that** the mod feels immersive and I can verify it's actually doing something.

**Implements**: FR-WORK-01, FR-WORK-02, FR-WORK-18, FR-NPC-01, FR-NPC-03

**Acceptance criteria (state — Gherkin):**
- **Given** a confirmed contract for today
  **When** the in-game clock reaches 6am
  **Then** the farmhand NPC spawns at the farm entrance tile and begins pathfinding toward the first task tile in the priority-ordered queue.

**Acceptance criteria (UI/visual — bullets):**
- The farmhand uses a placeholder sprite (recolored vanilla NPC) for v1.
- The farmhand has a visible energy bar so the player can understand remaining daily labor capacity at a glance.
- The farmhand visibly moves between tiles using `PathFindController`; no teleportation outside of building entry (FR-WORK-09) or stuck-recovery escalation (FR-WORK-12).
- The farmhand's movement speed is slower and more readable than the current instant-feeling implementation.
- The farmhand visibly swaps tools when changing task type (FR-WORK-10): axe for trees, watering can for crops, scythe for grass, pickaxe for rocks.
- Task beats are paced so the worker feels like in-world labor rather than instant automation.

---

### S-08 — Execute prioritized work across zones, buildings, and animals

**As** P-02 the Farmhand,
**I want** to perform tasks in a fixed priority order across the selected contract scope,
**so that** time-sensitive work (feeding/petting animals) happens before disruptive work (cutting trees).

**Implements**: FR-WORK-03, FR-TASK-03 through FR-TASK-12, FR-SKIP-04, FR-SKIP-05, FR-WORK-14

**Acceptance criteria (state — Gherkin):**
- **Given** a zone contains animals, mature crops, and trees
  **When** the farmhand enters the zone
  **Then** the task queue is built in priority order: Feed animals → Pet animals → Collect animal products → Water crops → Harvest crops → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees.
- **Given** the player selected a barn or coop for animal care
  **When** animals from that building are outdoors on the farm
  **Then** the farmhand still seeks them out and services them as part of the selected building's work.
- **Given** a tile contains a trellis crop surrounded by other trellis tiles
  **When** the farmhand reaches an adjacent tile
  **Then** the harvest is performed from the adjacent reachable side; if all adjacent tiles are unreachable, the crop is silently skipped (FR-SKIP-04).
- **Given** a crop is not yet ready to harvest
  **When** the harvest queue evaluates that tile
  **Then** the tile is silently skipped (FR-SKIP-05).
- **Given** the worker is on the "Clear grass" task and the silo is full
  **When** grass is cut
  **Then** hay is dropped on the ground at the worker's current tile and is never mailed (FR-TASK-09).

---

### S-09 — Snapshot tool capabilities at spawn and skip what can't be done

**As** P-02 the Farmhand,
**I want** to read the player's tool upgrade levels once at 6am and lock my capabilities to that snapshot for the shift,
**so that** my behavior is predictable even if the player swaps or upgrades tools mid-day, and so that I never claim to do work my tools can't do.

**Implements**: FR-TOOL-01, FR-TOOL-02, FR-TOOL-03, FR-TOOL-04, FR-SKIP-01, FR-SKIP-02, FR-SKIP-03

**Acceptance criteria (state — Gherkin):**
- **Given** the farmhand spawns at 6am
  **When** the capability snapshot is taken
  **Then** the upgrade level of Axe, Pickaxe, Watering Can, and Hoe is read from the player's tools and stored in the farmhand's per-shift state object. The player's tools are never held or consumed (FR-TOOL-04).
- **Given** the snapshot says axe level = Basic/Copper
  **When** the farmhand encounters a large log
  **Then** the log is silently skipped (FR-SKIP-01).
- **Given** the snapshot says pickaxe level = 0 (player does not own a pickaxe)
  **When** the contract includes "Clear rocks"
  **Then** all rock-clearing work is skipped for the day, and a mail warning is queued for the following morning (FR-TOOL-03).
- **Given** any axe level
  **When** the farmhand encounters a fruit tree
  **Then** the fruit tree is *always* skipped (FR-SKIP-03) — no exception.

---

### S-10 — Deposit collected items at shift end

**As** P-02 the Farmhand,
**I want** to deliver everything I collected to its designated chest (or shipping bin) before I leave,
**so that** no items are lost and the player wakes up to gold in the bin and items in the right boxes.

**Implements**: FR-WORK-05, FR-WORK-06, FR-WORK-07, FR-WORK-17, FR-OUT-01, FR-OUT-02, FR-OUT-03, FR-OUT-04, FR-OUT-05, FR-OUT-06, NFR-SAFE-01

**Acceptance criteria (state — Gherkin):**
- **Given** the farmhand has buffered items for tasks targeting chests C1 and C2 and the shipping bin
  **When** the shift ends (all tasks complete OR 8pm cap reached OR energy exhausted)
  **Then** the farmhand walks to C1, deposits all C1-bound items in one trip; walks to C2, deposits all C2-bound items in one trip; deposits shipping-bin items at the bin in one trip; then walks to the farm entrance and exits. The order of trips minimizes total walking distance but is otherwise unspecified.
- **Given** the 8pm cap has been reached
  **When** the farmhand has buffered items
  **Then** deposit runs still complete; items are never abandoned (FR-WORK-06, NFR-SAFE-01).
- **Given** the farmhand's energy reaches zero during a work unit
  **When** that work unit resolves
  **Then** the farmhand does not start a new work unit, but still completes deposit runs and exits normally.
- **Given** a target chest is full
  **When** the farmhand attempts to deposit
  **Then** as many items as fit are placed in the chest; the remainder stays in the buffer and is queued for next-morning mail (FR-OUT-02, FR-OUT-05).
- **Given** a target chest has been destroyed mid-shift
  **When** the farmhand reaches the chest's last known location and finds nothing
  **Then** all items destined for that chest go to next-morning mail and other tasks continue normally (FR-OUT-03).
- **Given** the farmhand exits via the farm entrance
  **When** the exit tile is reached
  **Then** no refund or additional billing is computed; the day was already charged at its explicit contract price.

---

### S-11 — Receive mail for overflow and unassigned output

**As** P-01 the Player,
**I want** any items the farmhand couldn't deliver to arrive in my mail the next morning with no fee,
**so that** "no items ever lost" is a real guarantee, not just a slogan.

**Implements**: FR-HIRE-10, FR-OUT-02, FR-OUT-03, FR-OUT-04, FR-OUT-05, NFR-SAFE-01, NFR-SAFE-02

**Acceptance criteria (UI/visual — bullets):**
- Mail arrives from `"Your farmhand"` (i18n-routed sender label per NFR-UX-02).
- The mail body briefly explains why items are attached (e.g., "Chest was full" / "No chest assigned" / "Chest no longer exists").
- All buffered items are attached to the mail with no fee, no penalty, and no hidden pricing adjustment.

**Acceptance criteria (state — Gherkin):**
- **Given** the farmhand exits with non-empty buffered items
  **When** the day rolls over (player sleeps and the next morning begins)
  **Then** exactly one mail letter is delivered carrying all buffered items.
- **Given** the destination is the shipping bin
  **When** the farmhand attempts to deposit
  **Then** no overflow can occur because the shipping bin has no capacity limit (FR-OUT-06); no mail is generated for shipping-bin items.

---

## Section 3 — Daily Life with a Recurring Contract

Once the player has a recurring contract running, ongoing management is light-touch.

### S-12 — Pause, cancel, or edit a recurring contract

**As** P-01 the Player,
**I want** to pause, cancel, or edit my recurring contract from the bulletin board without going through the full 4-screen flow,
**so that** I can adapt to changing plans (vacation, mining day, festival) without re-creating the contract.

**Implements**: FR-HIRE-12, FR-HIRE-15, FR-HIRE-16, FR-PAY-08, FR-PAY-09, FR-PAY-12, FR-PERSIST-01

**Acceptance criteria (UI/visual — bullets):**
- The bulletin board shows existing contracts with **Pause**, **Cancel**, and **Edit** actions.
- Editing returns the player to the appropriate hiring screen (tasks / zones / schedule) with current values pre-filled.
- Editing a recurring contract shows the revised fixed daily price before the player confirms the change.
- All controls work with mouse/keyboard *and* gamepad.

**Acceptance criteria (state — Gherkin):**
- **Given** a recurring contract is active
  **When** the player pauses or cancels it any time before 6am
  **Then** no daily contract charge is taken that morning and the farmhand does not show up.
- **Given** the player edits an active recurring contract before 6am
  **When** they confirm the edit
  **Then** the revised fixed daily price is shown before confirmation and applies on the next eligible contract day.
- **Given** the player attempts to cancel after 6am on a day the farmhand is already working
  **When** they select Cancel from the board
  **Then** the action is unavailable (mid-shift cancel is not supported per FR-HIRE-15); the shift runs to completion.
- **Given** a recurring contract is active and the player's gold drops below the fixed daily contract price before 6am
  **When** the next morning begins
  **Then** no charge is taken, the farmhand does not show up, and a same-day mail notification explains why.

---

### S-13 — Tune contract prices, worker stamina, and action costs in GMCM

**As** P-01 the Player,
**I want** to adjust contract prices, worker stamina, and action costs in GenericModConfigMenu,
**so that** the economic balance matches my save's difficulty or my personal taste.

**Implements**: FR-CFG-01, FR-WORK-13, FR-PAY-11, FR-PAY-12

**Acceptance criteria (UI/visual — bullets):**
- A Dayswork section appears in the GMCM mod list when GMCM is installed (optional dependency).
- Every spec-listed configurable value for the redesign (contract price values, worker energy capacity, per-action energy costs, 8pm cap, initial stuck threshold, post-teleport stuck threshold) is exposed as a labeled, validated, gamepad-friendly control.
- Labels and tooltips use i18n strings (NFR-UX-02).

**Acceptance criteria (state — Gherkin):**
- **Given** an active recurring contract is using pricing/energy set V1
  **When** the player edits pricing or energy values to V2 in GMCM during the day
  **Then** today's already-committed charge remains V1, and tomorrow's contract charge / worker energy behavior uses V2.

---

## Section 4 — Calendar & Edge Cases

The day doesn't always run smoothly. These stories cover days when normal flow breaks.

### S-14 — Handle festivals, rainy days, and low-work days without confusing contract behavior

**As** P-01 the Player,
**I want** the mod to do the right thing on weird days — festivals, rain, and low-work mornings — while keeping recurring pricing predictable,
**so that** I trust the recurring contract enough to leave it on for the whole season.

**Implements**: FR-DAY-01, FR-DAY-03, FR-PAY-09, FR-PAY-10

**Acceptance criteria (state — Gherkin):**
- **Given** today is a festival day (Egg Festival, Flower Dance, Spirit's Eve, etc.)
  **When** the in-game clock would reach 6am
  **Then** the farmhand does not show up, no daily charge is taken, and a same-day mail message explains the skipped work day.
- **Given** today's weather is rain and "Water crops" is the only enabled task
  **When** the day begins
  **Then** the contract price is unchanged; if there are no other actionable tasks, the worker may have little or nothing to do, but recurring pricing stays predictable.
- **Given** "Water crops" is among multiple enabled tasks and today is rainy
  **When** the day begins
  **Then** the farmhand shows up, rain-satisfied outdoor watering simply results in fewer actionable tasks, and the contract price does not change because of rain.
- **Given** the selected recurring contract scope contains zero or very little actionable work that morning (rocks already cleared, crops not ready, no animals currently needing service, etc.)
  **When** the day's contract begins
  **Then** the normal recurring contract charge still applies because that day's labor capacity was reserved.

---

### S-15 — Player sleeps before the farmhand finishes — shift settles cleanly before rollover

**As** P-01 the Player,
**I want** going to sleep early to settle the farmhand cleanly in the same "go to sleep?" beat,
**so that** I'm not blocked at bedtime and I never wake up to find the contract in a confusing half-finished state.

**As** P-02 the Farmhand (system actor),
**I want** sleep-confirm to stop my shift, settle any collected output, and exit before the day rolls over,
**so that** overflow handling is applied to *this* day's state, not the next.

**Implements**: FR-DAY-02, FR-OUT-05

**Acceptance criteria (state — Gherkin):**
- **Given** the player confirms sleep before the farmhand's shift would naturally end
  **When** the sleep transition begins
  **Then** before the day-rollover step, the farmhand stops taking on new work, settles collected-but-undelivered items, exits, and queues overflow mail (if any) for the next morning.
- **Given** remaining work existed at the moment the player slept
  **When** the next morning begins
  **Then** that unfinished work remains undone in the world, and only already-collected output has been settled.
- **Given** the same sleep scenario
  **When** the next morning begins
  **Then** overflow mail (if any) is in the player's mailbox, and no special refund step is required because pricing was already settled at contract charge time.

---

### S-16 — Recover from getting stuck (hybrid escalation)

**As** P-02 the Farmhand,
**I want** to recover from being stuck by trying a teleport before giving up,
**so that** a transient pathfinding glitch doesn't ruin the shift, but a player who fenced me in deliberately can still end the contract.

**Implements**: FR-WORK-11, FR-WORK-12, FR-WORK-13, NFR-SAFE-01

**Acceptance criteria (state — Gherkin):**
- **Given** the farmhand has made no movement and completed no task ticks for the configured stuck threshold (default: 10 in-game minutes)
  **When** the stuck detector fires for the first time in this shift
  **Then** (a) a confused emote ("?" balloon) is played, and (b) the farmhand attempts to teleport to the next reachable task tile in the priority queue.
- **Given** the farmhand is still stuck after another full stuck-threshold window post-teleport
  **When** the stuck detector fires again
  **Then** the farmhand teleports to the farm entrance and the shift ends as if the 8pm cap had been reached. Buffered items follow FR-OUT-02 / FR-OUT-03 (deposit where possible, mail the rest). The day remains charged at its explicit contract price.
- **Given** the player has tuned thresholds in GMCM
  **When** the next shift begins
  **Then** the new thresholds take effect (FR-WORK-13).

---

### S-17 — Survive player attacks without abandoning the shift

**As** P-02 the Farmhand,
**I want** to be invulnerable to player weapons but react visibly when hit,
**so that** accidental sword swings don't break the contract and intentional griefing doesn't yield any benefit.

**Implements**: FR-NPC-02

**Acceptance criteria (state — Gherkin):**
- **Given** the player swings a weapon and the hit registers on the farmhand
  **When** the hit is processed
  **Then** the farmhand takes 0 damage, plays a brief "ouch" / surprised emote, and resumes the current task without delay or path change.

---

### S-18 — Multiplayer refuses to load with a friendly message

**As** P-01 the Player who is in a multiplayer session with the mod installed,
**I want** the mod to gracefully decline rather than misbehave,
**so that** I don't have to uninstall just to play with friends.

**Implements**: FR-MP-01

**Acceptance criteria (state — Gherkin):**
- **Given** the mod loads and detects a multiplayer session (host or peer)
  **When** mod initialization runs
  **Then** the bulletin-board patch is not applied (the option is hidden, per S-01), and a friendly informational message is written to the SMAPI log explaining that Dayswork is single-player only in v1.

---

## Section 5 — Maintainability

These stories anchor the architectural choices that keep the mod testable and translatable.

### S-19 — Pure logic separable from SMAPI for testability

**As** P-03 the Mod Maintainer,
**I want** contract pricing, energy-cost accounting, zone-tile intersection, capability evaluation, and save-data DTOs to live in plain C# classes with no SMAPI or game-engine dependencies,
**so that** I can unit-test them with xUnit and property-test them with FsCheck without launching Stardew Valley.

**Implements**: NFR-MAINT-01, NFR-MAINT-02, NFR-MAINT-03, NFR-MAINT-04

**Acceptance criteria (UI/visual — bullets):**
- Solution layout has at least two projects: the SMAPI mod (`Dayswork`) and a test project (`Dayswork.Tests`) using xUnit + FsCheck.
- All pure-logic types compile and tests run without any SMAPI / Stardew assemblies on the test classpath.
- Harmony patches live in a single, isolated namespace (e.g., `Dayswork.Patches`) so conflicts can be diagnosed by file location alone.

**Acceptance criteria (state — Gherkin) — PBT obligations:**
- **Given** the contract-pricing function
  **When** FsCheck generates valid task-selection sets, scope-band inputs, building selections, greenhouse selections, and pricing-config inputs
  **Then** the resulting contract price is deterministic for the same saved scope and selected services, and the same input set always produces the same price (PBT-03 invariant).
- **Given** the worker-energy accounting function
  **When** FsCheck generates valid action sequences and per-action energy costs
  **Then** energy never drops below zero, and no new work unit begins once the energy state has reached zero (PBT-03 invariant; NFR-SAFE-02 integrity).
- **Given** save-data DTOs
  **When** FsCheck generates valid contract states
  **Then** `deserialize(serialize(state)) == state` holds for all generated inputs (PBT-02 round-trip).
- **Given** the seed value
  **When** a property test fails
  **Then** the seed and the shrunk minimal failing input are logged so the case can be replayed deterministically (PBT-08).

---

### S-20 — Externalize all user-visible strings for community translation

**As** P-03 the Mod Maintainer,
**I want** every string that an end user can see to live in `i18n/default.json` rather than in C# source,
**so that** community translators can submit new languages as JSON-only PRs without touching .NET.

**Implements**: NFR-UX-02, FR-CFG-02

**Acceptance criteria (UI/visual — bullets):**
- A code-search of the mod assembly for hardcoded English strings shown to users (menu labels, error messages, mail letter bodies, GMCM tooltips, log-warning sentences shown to players) returns zero matches.
- All user-visible strings are loaded via SMAPI's i18n helper (`I18n.Get(...)` or equivalent) referencing keys defined in `i18n/default.json`.
- v1 ships with English (`default.json`); the structure permits dropping additional `i18n/{locale}.json` files without code changes.

---

## Coverage Summary

| Requirement group | Stories covering it |
|---|---|
| §2.1 Hiring entry point and menu (FR-HIRE-01..16) | S-01, S-02, S-03, S-04, S-05, S-06, S-12 |
| §2.2 Tasks (FR-TASK-01..12) | S-02, S-03, S-04, S-08 |
| §2.3 Worker arrival/shift loop (FR-WORK-01..19) | S-07, S-08, S-09, S-10, S-13, S-15, S-16 |
| §2.4 Skipped objects (FR-SKIP-01..05) | S-08, S-09 |
| §2.5 Tool inheritance (FR-TOOL-01..04) | S-09 |
| §2.6 Output, deposit, fallback (FR-OUT-01..07) | S-10, S-11 |
| §2.7 Pricing (FR-PAY-01..12) | S-02, S-06, S-12, S-13, S-14, S-19 |
| §2.8 Day & calendar edges (FR-DAY-01..03) | S-14, S-15 |
| §2.9 Worker NPC behavior (FR-NPC-01..03) | S-07, S-17 |
| §2.10 Persistence (FR-PERSIST-01..02) | S-05, S-12 |
| §2.11 Multiplayer (FR-MP-01) | S-01, S-18 |
| §2.12 Config & UX (FR-CFG-01..02) | S-13, S-20 |
| §2.13 Mod compatibility (FR-COMPAT-01..02) | (docs-only; no story) |
| Maintainability NFRs (NFR-MAINT-01..05) | S-19 |
| Onboarding NFRs (NFR-ONBOARD-01..02) | (covered by docs; no story) |
| Safety / data-integrity NFRs (NFR-SAFE-01..04) | S-10, S-11, S-16, S-19 |

No FR group is left uncovered. The two "covered by docs, no story" lines (mod compatibility and onboarding) are intentional — they're documentation deliverables rather than user-observable behavior.
