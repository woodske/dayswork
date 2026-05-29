# User Stories — Dayswork

**Personas**: see [personas.md](personas.md). P-01 = Player, P-02 = Farmhand, P-03 = Mod Maintainer.

**Organization**: Stories are grouped by **user journey** — the natural flow of a player encountering and using the mod over time. Within each section, stories are ordered as they would unfold in real play.

**Story format**: `As [persona], I want [capability], so that [benefit]`.

**Acceptance criteria format**: **Gherkin** (Given/When/Then) for behaviors involving state transitions; **bullets** for UI/visual rules and simple presence checks.

**Traceability**: Each story lists the FR-IDs from [requirements.md](../requirements/requirements.md) that it implements. Worker-routing updates also reference [worker-routing-requirements.md](../requirements/worker-routing-requirements.md). All v1 FRs are covered across the story set; no prioritization markers per Q8 of the planning document.

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
- **SVE note:** on supported SVE farm maps, the 6am spawn tile and shift-end exit are resolved per **S-22** (the `Farm.warps` heuristic plus a verified per-map override); arrival and departure are otherwise identical to vanilla.

---

### S-08 — Execute prioritized local work across zones, buildings, and animals

**As** P-02 the Farmhand,
**I want** to respect the broad contract work order while choosing the closest reachable work inside the active batch,
**so that** time-sensitive work still happens first and I do not walk past obvious nearby chores.

**Implements**: FR-WORK-03, FR-TASK-03 through FR-TASK-12, FR-SKIP-04, FR-SKIP-05, FR-WORK-14, FR-WR-01, FR-WR-02, FR-WR-03, FR-WR-04, FR-WR-05, FR-WR-07, FR-WR-08

**Acceptance criteria (state — Gherkin):**
- **Given** a zone contains animals, mature crops, and trees
  **When** the farmhand enters the zone
  **Then** broad batch order is preserved: animal building work, outdoor animal work, greenhouse work, outdoor crop work, then outdoor clearing work.
- **Given** the farmhand is inside an active broad batch with multiple reachable tasks
  **When** the next task is selected
  **Then** the farmhand chooses the task with the shortest reachable route from its current tile, using task priority only as the equal-distance tie-breaker.
- **Given** the player selected a barn or coop for animal care
  **When** animals from that building are outdoors on the farm
  **Then** the farmhand still seeks them out and services them as part of the selected building's work.
- **Given** two animals in the active animal batch need attention
  **When** one animal is closer by reachable route than the other
  **Then** the farmhand services the closer reachable animal before walking to the farther one.
- **Given** an adjacent-interaction task such as a weed, tree, fruit tree, rock, or animal product has more than one valid stand tile
  **When** the farmhand chooses where to stand
  **Then** the stand tile is the reachable side with the shortest route from the farmhand's current tile, not a fixed top-first side.
- **Given** the farmhand is already standing on a valid interaction tile for the next task
  **When** the task is selected
  **Then** the farmhand performs the task from that tile instead of walking around to another side of the same object.
- **Given** an egg or other animal-product floor item has one blocked side and another reachable side
  **When** `CollectAnimalProducts` is enabled and the product is in the active batch
  **Then** the farmhand collects the product from the reachable side rather than abandoning it because the preferred side is blocked.
- **Given** a tile contains a trellis crop surrounded by other trellis tiles
  **When** the farmhand reaches an adjacent tile
  **Then** the harvest is performed from the adjacent reachable side; if all adjacent tiles are unreachable, the crop is silently skipped (FR-SKIP-04).
- **Given** a crop is not yet ready to harvest
  **When** the harvest queue evaluates that tile
  **Then** the tile is silently skipped (FR-SKIP-05).
- **Given** feed work is blocked by eggs or other animal products and `CollectAnimalProducts` is not enabled
  **When** the farmhand evaluates the feed route
  **Then** the farmhand does not silently collect those products as unpaid work, and feeding may remain incomplete if no enabled work clears the path.
- **Given** feed work is blocked by eggs or other animal products and `CollectAnimalProducts` is enabled
  **When** enabled product collection clears the path later in the same animal-building batch
  **Then** the farmhand retries the deferred feed work before leaving that batch.
- **Given** the worker is on the "Clear grass" task and the silo is full
  **When** grass is cut
  **Then** hay is dropped on the ground at the worker's current tile and is never mailed (FR-TASK-09).

**Acceptance criteria (UI/visual — bullets):**
- The farmhand should visibly behave like it is choosing sensible nearby work inside the current work area.
- The farmhand should not appear to walk around an object just to use a different side when its current side is already valid.
- **SVE note:** when SVE premium barns/coops are part of the contract, animal servicing follows **S-23** — feeding sizes to the building's real capacity, and pet/collect simply find nothing to do for animals an auto-petter/auto-grabber has already handled (no machine-presence assumption is made).

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

**Implements**: FR-WORK-11, FR-WORK-12, FR-WORK-13, NFR-SAFE-01, FR-WR-06, FR-WR-08

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
- **Given** a task's selected route is blocked but there is other work remaining in the same active batch
  **When** navigation to that task fails
  **Then** the task is deferred temporarily and the farmhand attempts other available work in the batch before retrying it.
- **Given** a deferred task may have become reachable because other enabled work changed the world
  **When** the farmhand reaches the retry point for that batch
  **Then** the task is rechecked using current passability and can be completed if a route is now available.
- **Given** a deferred task is still unreachable after retry
  **When** no further work in the batch can change that route
  **Then** the task is skipped for the day without trapping the farmhand in an infinite loop.

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

**Implements**: NFR-MAINT-01, NFR-MAINT-02, NFR-MAINT-03, NFR-MAINT-04, NFR-WR-02, NFR-WR-04

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
- **Given** the worker route-ordering helper
  **When** FsCheck generates valid current positions, candidate tasks, candidate interaction tiles, and deterministic tie conditions
  **Then** the selected task is always the reachable candidate with the shortest route length, and equal route lengths resolve by task priority and stable ordering (PBT-03 invariant).
- **Given** the blocked-task deferral helper
  **When** FsCheck generates finite batches with reachable, temporarily blocked, and permanently blocked work
  **Then** deferred work is retried after progress opportunities and the selection process always terminates without cycling forever (PBT-03 invariant).
- **Given** the user-reported routing regressions
  **When** example-based tests run
  **Then** they pin the cases for wrong-side walking, one-side-blocked egg collection, near animal before far animal, hopper blocked by eggs, and retry after clearing work.
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

### S-26 — Add expansion compatibility by writing one isolated provider

**As** P-03 the Mod Maintainer,
**I want** SVE-specific behavior isolated behind an expansion-compatibility provider with a vanilla default,
**so that** I can support another expansion by writing one new provider, keep the vanilla path untouched, and unit/property-test the pure compatibility logic without launching Stardew.

**Implements**: FR-SVE-01, FR-SVE-04, NFR-SVE-02, NFR-SVE-03, NFR-SVE-05, NFR-SVE-06, NFR-SVE-07 (companion to the SVE journey stories S-21..S-25 in Section 6)

**Acceptance criteria (UI/visual — bullets):**
- A single provider abstraction defines the compatibility surface: expansion detection, worker-entrance resolution, animal-building capacity/feeding derivation, the building work-location set, and content-classification overrides.
- A **Vanilla** provider implements today's default behavior; an **SVE** provider implements only the overrides. No vanilla/core call site contains SVE-specific branches.
- All SVE-specific identifiers (mod ID, building data keys, map/location names, per-map entrance overrides) are centralized in the SVE provider, not scattered as magic strings (NFR-SVE-07).
- Adding a hypothetical new expansion requires implementing and registering a new provider only — no edits to vanilla/core call sites (NFR-SVE-02).
- The pure compatibility logic compiles and its tests run without SMAPI / Stardew assemblies on the classpath (consistent with S-19).
- Every SVE mapping is grounded in SVE source or vanilla behavior before implementation; no assumptions (NFR-SVE-03).

**Acceptance criteria (state — Gherkin) — PBT obligations:**
- **Given** the provider-selection function
  **When** FsCheck generates sets of installed mod IDs (with and without the SVE ID)
  **Then** exactly one provider is selected deterministically, the Vanilla provider is chosen whenever no recognized expansion is present, and selection is stable for the same input (PBT-03 invariant).
- **Given** the worker-entrance resolution function
  **When** FsCheck generates warp configurations and optional per-map overrides
  **Then** a reachable entrance tile is always produced (override when present, else heuristic, else documented fallback), deterministically (PBT-03).
- **Given** the animal-building capacity-derivation function
  **When** FsCheck generates trough/occupant data for vanilla and premium buildings
  **Then** derived feed capacity equals the actual trough/occupant-based count and never the legacy hardcoded constant for non-vanilla buildings (PBT-03).
- **Given** the content-classification override function
  **When** FsCheck generates known and unknown content descriptors
  **Then** known content maps to the correct task/capability and unknown content maps to "skip" without ever throwing (PBT-03).
- **Given** a property-test failure
  **When** it is reported
  **Then** the seed and the shrunk minimal failing input are logged for deterministic replay (PBT-08).

**Acceptance criteria (performance):**
- **Given** provider lookups occur in runtime hot paths
  **When** the worker runs a shift
  **Then** the active provider is resolved once / cached and introduces no per-tile reflection or per-frame mod-registry queries; runtime stays within the Worker Routing performance envelope (NFR-SVE-06). *(Validated via the existing performance scenario plus manual SVE playtest.)*

---

## Section 6 — Expansion Compatibility (Stardew Valley Expanded)

These stories describe how Dayswork behaves when **Stardew Valley Expanded** is installed, while leaving vanilla behavior unchanged. The maintainer-facing companion (the provider seam itself) is **S-26**, kept in Section 5 alongside the other architecture stories.

> **Grounding & validation note:** Per NFR-SVE-03, every SVE-specific detail (mod ID, entrance tiles, building keys, Grandpa's Shed interior, custom content) is confirmed against SVE source during design — none is assumed here. Per NFR-SVE-05, criteria that depend on SVE assets being loaded are marked *"validated via manual SVE playtest"*, while the pure compatibility logic is covered by xUnit + FsCheck (see S-26).

### S-21 — Vanilla stays vanilla; SVE support turns on automatically

**As** P-01 the Player,
**I want** Dayswork to behave exactly as it does today when I have no expansion installed, and to adapt automatically when Stardew Valley Expanded is present,
**so that** I never configure compatibility and my non-SVE saves are completely unaffected.

**Implements**: FR-SVE-01, FR-SVE-02, FR-SVE-03, NFR-SVE-01

**Acceptance criteria (state — Gherkin):**
- **Given** no recognized expansion mod is installed
  **When** Dayswork loads and runs any contract
  **Then** the Vanilla provider is active and every observable behavior matches the current release. *(Validated via the existing regression suite + vanilla playtest.)*
- **Given** SVE is installed
  **When** Dayswork starts up
  **Then** SVE is detected through its mod ID in the SMAPI mod registry and the SVE provider is activated for the session. *(Validated via a unit test of provider selection + manual SVE playtest.)*
- **Given** SVE was installed and is later removed
  **When** Dayswork next loads without it
  **Then** it falls back to the Vanilla provider with no error and no residual SVE state.

**Acceptance criteria (UI/visual — bullets):**
- Dayswork declares **no** SVE dependency in its `manifest.json`; it loads with or without SVE present.
- The active provider is logged once at startup at debug level for maintainer diagnosis.

---

### S-22 — The farmhand arrives correctly on SVE farm maps

**As** P-01 the Player (and **P-02** the Farmhand),
**I want** the farmhand to spawn at and exit from a sensible entrance on the supported SVE farm maps — **Immersive Farm 2 Remastered, Grandpa's Farm, and Frontier Farm** — and to skip tiles it can't reach,
**so that** hiring works on my SVE farm just like on a vanilla farm.

**Implements**: FR-SVE-05, FR-SVE-06, FR-SVE-15

**Acceptance criteria (state — Gherkin):**
- **Given** the active farm is one of the three supported SVE maps
  **When** a contract day begins at 6am
  **Then** the farmhand spawns at a reachable entrance tile appropriate to that map and begins work. *(Validated via manual SVE playtest on each supported map.)*
- **Given** the `Farm.warps` "first outdoor warp" heuristic would resolve a wrong or unreachable entrance on a supported SVE map
  **When** the entrance is resolved
  **Then** the SVE provider supplies a verified per-map override (grounded in that map's warp/source data) and the worker uses it.
- **Given** a drawn zone on an SVE map overlaps tiles the worker cannot reach (water, cliffs, custom terrain)
  **When** the worker executes
  **Then** those tiles are silently skipped (FR-SVE-15) exactly as on vanilla — no crash, no warning.

**Acceptance criteria (UI/visual — bullets):**
- On each supported SVE map the worker visibly walks in from the entrance and out at shift end (no extra warp-in/out beyond the existing building-entry behavior).
- GrampletonFields is out of scope for this change; it is not offered as a work area.

---

### S-23 — Premium Barn and Premium Coop are fully serviced

**As** P-02 the Farmhand,
**I want** to feed, pet, and collect from SVE Premium Barns and Premium Coops correctly — including filling all troughs in a 16-animal building —
**so that** players with premium buildings get the same service quality as with vanilla buildings.

**Implements**: FR-SVE-07, FR-SVE-08, FR-SVE-09, FR-SVE-10, FR-SVE-11

**Acceptance criteria (state — Gherkin):**
- **Given** a Premium Barn or Premium Coop (an `AnimalHouse` with `MaxOccupants` 16) is selected for animal care
  **When** the worker services it with sufficient silo hay
  **Then** feeding fills up to the building's **actual** capacity (derived from its real trough tiles / building data), not the legacy hardcoded 4. *(Capacity derivation covered by unit/PBT tests; end-to-end validated via manual SVE playtest.)*
- **Given** an AutoPetter and/or AutoGrabber is present and has already petted animals / grabbed produce
  **When** the worker scans the building for pet/collect work
  **Then** it finds nothing to do for those animals and moves on — it does **not** detect or special-case the machines (the player may relocate or remove them).
- **Given** those machines are absent, removed, or have not yet acted
  **When** the worker scans
  **Then** it pets un-petted animals and collects available produce normally.
- **Given** the premium building turns out to auto-feed (to be confirmed from SVE building/map source)
  **When** the worker evaluates feed work
  **Then** full troughs yield no feed work (natural skip); where it does not auto-feed, the worker feeds to capacity.

**Acceptance criteria (UI/visual — bullets):**
- SVE premium animal buildings are selectable wherever the hiring UI enumerates animal buildings; the scope model accommodates premium tiers, not only the six vanilla tiers. *(Validated via manual SVE playtest of the hiring UI.)*
- No code assumes an auto-petter or auto-grabber exists in any building.

---

### S-24 — New SVE crops, trees, animals, and products just work (or skip safely)

**As** P-02 the Farmhand,
**I want** SVE's new crops, trees, animals, and animal products handled through the same data-driven logic as vanilla, with anything I genuinely can't classify skipped safely,
**so that** players get useful work on SVE content with no lost items and no crashes.

**Implements**: FR-SVE-12, FR-SVE-13, FR-SVE-15, FR-SVE-16, NFR-SVE-04

**Acceptance criteria (state — Gherkin):**
- **Given** SVE crops growing in tilled dirt
  **When** harvest runs
  **Then** they harvest through the existing `HoeDirt`/`Crop` path like vanilla crops. *(Validated via manual SVE playtest.)*
- **Given** an SVE animal with a tool-harvest product (e.g., a new milk- or wool-type animal)
  **When** collection runs
  **Then** the product is collected via `currentProduce` + `ItemRegistry`, and milk/shear classification covers the new animal type (verified against SVE source). *(Classification covered by unit tests; end-to-end via manual SVE playtest.)*
- **Given** SVE trees that are vanilla `Tree`/`FruitTree` instances
  **When** the worker evaluates them
  **Then** standing trees are chopped and fruit trees are always skipped, like vanilla.
- **Given** a custom SVE `ResourceClump` or tree species whose identity is not recognized
  **When** the worker evaluates it
  **Then** it is classified if confirmed from SVE source, otherwise silently skipped (FR-SVE-15) without crashing.
- **Given** the worker has buffered SVE items it cannot deposit
  **When** the shift ends
  **Then** existing overflow-to-mail safety applies and no item is lost (FR-SVE-16).

**Acceptance criteria (UI/visual — bullets):**
- Explicit per-content handling is added only at gaps confirmed in SVE source; generic content flows through the unchanged data-driven path.
- Unclassifiable content is logged at debug/trace for maintainers, never surfaced to the player.

---

### S-25 — Grandpa's Shed is a usable work location

**As** P-01 the Player (and **P-02** the Farmhand),
**I want** the farmhand to treat Grandpa's Shed as a work location,
**so that** the tasks its interior supports — and any chests inside it — are handled like other farm buildings.

**Implements**: FR-SVE-14, FR-SVE-16

**Acceptance criteria (state — Gherkin):**
- **Given** Grandpa's Shed is built and within the contract's scope
  **When** the worker runs
  **Then** it navigates into the shed (door/warp and entry tile resolved from the SVE map source) and performs whatever applicable tasks its interior supports. *(Validated via manual SVE playtest.)*
- **Given** chests inside Grandpa's Shed are assigned as output destinations
  **When** the worker deposits
  **Then** it makes a deposit trip into the shed and places items, falling back to overflow mail if the chest is unreachable or full (FR-SVE-16). *(Validated via manual SVE playtest.)*
- **Given** the shed interior supports indoor crops (to be confirmed from SVE source)
  **When** crop tasks are enabled
  **Then** the worker waters/harvests them as for any indoor crop area.

**Acceptance criteria (UI/visual — bullets):**
- Grandpa's Shed appears as a selectable building wherever the hiring UI enumerates buildings (when it exposes chests or work). *(Validated via manual SVE playtest.)*
- Its exact interior contents and supported task set are confirmed from SVE source, not assumed (NFR-SVE-03).

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
| §2.13 Mod compatibility (FR-COMPAT-01..02) | general docs; SVE compatibility now covered by S-21..S-26 |
| SVE detection & vanilla invariance (FR-SVE-01/02/03; NFR-SVE-01) | S-21, S-26 |
| SVE farm maps & worker entrance (FR-SVE-05/06; FR-SVE-15) | S-22 |
| SVE premium barn/coop (FR-SVE-07..11) | S-23 |
| SVE new crops/trees/animals/products (FR-SVE-12/13; FR-SVE-15; NFR-SVE-04) | S-24 |
| SVE Grandpa's Shed (FR-SVE-14; FR-SVE-16) | S-25 |
| SVE isolation/extensibility/testability (FR-SVE-04; NFR-SVE-02/03/05/06/07) | S-26 |
| Maintainability NFRs (NFR-MAINT-01..05) | S-19 |
| Onboarding NFRs (NFR-ONBOARD-01..02) | (covered by docs; no story) |
| Safety / data-integrity NFRs (NFR-SAFE-01..04) | S-10, S-11, S-16, S-19 |

No FR group is left uncovered. SVE compatibility (FR-SVE-* / NFR-SVE-*) is now covered by S-21..S-26, expanding the formerly docs-only mod-compatibility row. The remaining "covered by docs, no story" line (onboarding) is intentional — it's a documentation deliverable rather than user-observable behavior.
