# Dayswork — Design & Technical Specification

Stardew Valley · SMAPI Mod · C# / .NET · v0.1 draft

> **Source**: Verbatim copy of `C:\Users\kwood\Downloads\dayswork-mod-spec.md` provided by the user as the starting point for AI-DLC inception. This is the input artifact; derived requirements live in `aidlc-docs/inception/requirements/requirements.md` once analysis completes.

---

## Overview

A Stardew Valley SMAPI mod that lets players hire generic worker NPCs from the town bulletin board. Workers physically walk onto the farm, complete assigned tasks within designated zones, and leave when done or when their shift ends. Payment is handled via an upfront deposit, refunded if work finishes early.

### Design goals

- **Immersive** — a physical NPC walks around and performs tasks visually
- **Player-controlled** — task types, zones, and output destinations are all configurable
- **Progression-aware** — worker capability is tied to the player's tool upgrade level
- **Lore-friendly** — entry point through the existing bulletin board, no new buildings required for v1
- **Safe** — no items are ever lost; overflow goes to mail

---

## Player flow

| Step | Action | Detail |
|------|--------|--------|
| 1 | Open bulletin board | Player interacts with the town bulletin board in Pelican Town |
| 2 | Open hiring menu | "Hire a Farmhand" option appears on the board, opening the mod's hiring UI |
| 3 | Select tasks | Toggle which tasks the worker can perform; hourly rate updates live |
| 4 | Define zones | Draw rectangles over farm tiles and/or select buildings; assign output chests per task |
| 5 | Choose schedule | One-time hire or recurring daily contract |
| 6 | Review & confirm | Summary screen shows estimated hours, deposit, and refund policy; confirming deducts deposit |
| 7 | Worker arrives | Worker spawns at the farm entrance at 6am the following morning |
| 8 | Deposit & refund | At shift end, worker deposits items into designated chests, exits, and refund is applied |

---

## Hiring menu

### Entry point

The bulletin board in Pelican Town is patched via Harmony to inject a "Hire a Farmhand" option. Selecting it opens the mod's custom hiring UI, which consists of four screens navigated sequentially.

### Screen 1 — Task selection

- Displays all available tasks as toggles
- Hourly rate updates live as tasks are enabled/disabled
- Base rate is always charged regardless of task selection
- Each task shows its rate contribution next to its toggle

### Screen 2 — Zone & output configuration

- Player enters zone-drawing mode directly from this screen
- Two zone types: **tile rectangles** (click and drag on farm) and **buildings** (click to select)
- Multiple zones and buildings can be combined in one contract
- Each task that produces output shows a "Set output chest" button
- Chest assignment uses a split model depending on chest location:
  - **Open farm chests** — player clicks the chest directly on the farm map while in zone-drawing mode, same interaction as selecting a tile
  - **Building chests** (greenhouse, shed, barn, coop, etc.) — shown in a dropdown panel grouped by building; player selects from the list without needing to be physically inside the building
- Building chest labels use the chest's in-game name if set, otherwise fall back to tile coordinates within that building (e.g. "Greenhouse — Chest at 5, 8")
- Buildings with no chests are omitted from the dropdown
- Chest is identified by location name + tile coordinates — renaming the chest won't break the assignment, but moving it will
- Multiple tasks can share the same chest
- If no chest is assigned, output is mailed at end of shift (no penalty)

### Screen 3 — Schedule

- **One-time:** worker is hired for the next morning only
- **Recurring:** worker is hired each morning automatically; deposit deducted daily
- Recurring contracts can be paused or cancelled from the bulletin board any time before 6am
- Zones and task settings persist between days and are editable

### Screen 4 — Summary & confirm

- Shows: selected tasks, estimated hours, hourly rate, total deposit, refund policy
- Confirming deducts the deposit immediately
- If the player cannot afford the deposit, confirmation is blocked with an error message

---

## Tasks — v1 scope

| Task | Output destination | Notes |
|------|--------------------|-------|
| Water crops | None | No output produced |
| Harvest crops | Shipping bin or chest (player choice) | Configured per task in hiring menu |
| Collect fruit | Shipping bin or chest (player choice) | Fruit trees only — not felled |
| Feed animals | None | Consumes hay from silo |
| Pet animals | None | No output produced |
| Collect animal products | Designated chest | Eggs, milk, wool, truffles, etc. |
| Cut trees | Designated chest | Wood, sap, seeds. Fruit trees always excluded. |
| Clear rocks | Designated chest | Stone, ore, geodes, gems — no filtering |
| Clear weeds | Designated chest | Fiber, mixed seeds |
| Clear grass | Silo (if space), else drop on ground | Matches vanilla scythe behavior |

> **Note:** Fruit trees are always excluded from the cut trees task regardless of zone selection. This is a hard rule, not a toggle, in v1.

---

## Pricing model

### Rate structure

Each task adds a flat amount to the worker's hourly rate. The base rate is always charged. All rates are configurable via GenericModConfigMenu and `config.json`.

| Component | Rate (g/hr) | Notes |
|-----------|-------------|-------|
| Base rate | 50g | Always charged |
| Water crops | +20g | |
| Harvest crops | +25g | |
| Collect fruit | +15g | |
| Feed animals | +20g | |
| Pet animals | +10g | |
| Collect animal products | +15g | |
| Cut trees | +30g | Higher rate — more labor |
| Clear rocks | +20g | |
| Clear weeds | +20g | |
| Clear grass | +20g | |

### Deposit calculation

- Deposit = hourly rate × estimated hours
- Estimated hours are calculated based on zone size, number of tasks, and a configurable average speed constant
- Deposit is deducted at confirmation (one-time) or each morning at 6am (recurring)
- If the player cannot afford the daily deposit on a recurring contract, the worker does not show up and a mail notification is sent
- Unused deposit is refunded at shift end: `(actual hours worked) × hourly rate`, deposited directly into player gold

> **Note:** Deposit time (walking to chests at end of shift) is not billed. The hourly rate covers active task work only.

---

## Worker behavior

### Arrival & departure

- Worker spawns at the farm entrance at 6am
- Worker pathfinds between task tiles using the game's built-in `PathFindController`
- Shift ends when all tasks in all zones are complete, or at the 8pm hard cap
- After shift end, worker walks to each designated chest and deposits all buffered items (one trip per unique chest)
- If the 8pm cap is hit mid-task, the worker still completes deposit runs before leaving — items are never lost
- Worker exits via the farm entrance after all deposits are complete
- Refund is calculated and deposited to player gold at the moment the worker exits

### Shift loop — zone execution

1. **Enter zone** — worker pathfinds to the first task tile in the zone
2. **Build task queue** — scan zone tiles for actionable objects, filtered by enabled tasks and tool capability
3. **Execute tasks** — worker performs tasks in priority order, collecting all drops into an internal item buffer
4. **Move to next zone** — repeat until all zones are complete
5. **Deposit all items** — worker walks to each unique designated chest and deposits buffered items
6. **Exit farm** — worker leaves via farm entrance; refund is applied

> If a zone contains tasks with different designated chests (e.g. crops to chest A, rocks to chest B), the worker makes two deposit trips at end of shift — one per unique chest.

### Task priority order

Within a zone, tasks are executed in this order:

1. Feed animals *(time-sensitive for happiness)*
2. Pet animals
3. Collect animal products
4. Water crops
5. Harvest crops
6. Collect fruit
7. Clear weeds
8. Clear grass
9. Clear rocks
10. Cut trees *(most disruptive — last)*

### Skipped object behavior

- **Stumps or logs** the worker cannot chop (axe level too low): silently skipped, worker moves to next tree
- **Boulders** the worker cannot break (pickaxe level too low): silently skipped
- **Fruit trees**: always skipped regardless of axe level or zone selection
- **Trellis crops**: worker harvests from adjacent reachable tiles, skips if surrounded
- **Unready crops**: skipped if not yet ready to harvest

---

## Tool inheritance

At 6am when the worker spawns, the mod reads the upgrade level of the player's tools and stores a snapshot in the worker's state object. The worker's capability for that shift is locked to this snapshot — it does not update if the player changes tools mid-day.

| Tool | Upgrade level | Worker can do | Worker skips |
|------|--------------|---------------|--------------|
| Axe | Basic / Copper | Standing trees, small stumps | Large stumps, large logs |
| Axe | Steel | Standing trees, stumps | Large logs |
| Axe | Gold+ | All trees, stumps, large logs | Nothing |
| Pickaxe | Basic / Copper | Small rocks, small boulders | Large boulders, meteorites |
| Pickaxe | Steel | Small rocks, large boulders | Meteorites |
| Pickaxe | Gold+ | All rocks, boulders, meteorites | Nothing |
| Watering can | Any | Waters all crops in zone tile-by-tile | — |
| Hoe | Any | Not used in v1 | — |

Tool level is read from `Game1.player.getToolFromName("Axe").UpgradeLevel` at spawn. The worker never holds or consumes the player's tools — upgrade level is a read-only capability check. If the player does not own a tool (e.g. sold their axe), treat level as 0 and skip all tasks requiring it. A mail warning is sent the following morning.

---

## Output & materials

### Deposit behavior

- Worker holds all collected items in an internal buffer for the entire shift
- At end of shift, worker makes one trip per unique designated chest to deposit
- Deposit happens after all task work is complete — deposit time is not billed
- Items from different tasks going to the same chest are deposited in one trip

### Chest full or missing — fallback

- **Chest full:** worker deposits what fits, buffers the overflow
- **Chest removed:** worker buffers all output for that task
- **No chest assigned:** worker still completes the task and buffers all drops
- All buffered overflow is mailed to the player the following morning — no fee, no penalty
- Mail arrives from "Your farmhand" with a brief note and all items attached
- Shipping bin has no capacity limit in vanilla — overflow cannot occur when the bin is the destination

### Hay — special case

When clearing grass, the worker attempts to store hay in the player's silo first, matching vanilla scythe behavior. If the silo is full, hay is dropped on the ground at the worker's current position. If the player has no silo, grass is cleared but no hay is produced. Hay is never mailed — dropping on the ground matches the vanilla precedent and avoids item duplication via mail.

### No item filtering

All drops from a task go into the single designated chest for that task with no sorting or filtering. Rock clearing sends stone, ore, geodes, and gems all to the same chest. This is intentional for v1 simplicity.

---

## Technical architecture

### Key components

| Component | Implementation |
|-----------|---------------|
| Bulletin board patch | Harmony postfix on `BulletinBoard` menu draw + click handler |
| Hiring menu UI | Custom `IClickableMenu` subclass — 4 screens |
| Zone drawing mode | `DisplayEvents` render hook + mouse drag state tracking |
| Chest assignment — open farm | Clickable chest detection in zone-drawing mode, stored as tile coordinates |
| Chest assignment — buildings | Dropdown panel populated via `Utility.ForAllLocations`, stored as location name + tile coordinates |
| Worker NPC | Custom class extending `NPC`, overrides `update()` loop |
| Task queue | Priority-ordered queue built from zone tile scan at shift start |
| Pathfinding | Game's built-in `PathFindController` |
| Time tracking | SMAPI `TimeChanged` event |
| Tool snapshot | Read at 6am spawn, stored in worker state object |
| Contract persistence | SMAPI data/save API (stored per-save) |
| Config UI | GenericModConfigMenu (GMCM) |
| Mail system | SMAPI mail API for overflow items and warning notices |

### Suggested build order

1. **Project scaffold** — SMAPI boilerplate, manifest, mod entry point
2. **Bulletin board patch** — inject menu option, open placeholder UI
3. **Hiring menu screens** — task toggles, live rate display, schedule picker, summary
4. **Zone drawing mode** — tile overlay rendering, rectangle selection, building selection
5. **Chest assignment** — selection mode, coordinate persistence
6. **Worker NPC** — spawn at farm entrance, walk to a tile, perform one task, leave
7. **Task queue system** — tile scanning, priority ordering, tool capability checks
8. **Item buffer & deposit** — end-of-shift deposit run, chest pathfinding
9. **Payment system** — deposit deduction, refund calculation, mail fallback
10. **Contract persistence** — save/load contracts via SMAPI data API
11. **Recurring contracts** — daily deposit, pause/cancel from bulletin board
12. **Config** — expose all rates and constants to GMCM

---

## Open questions

- **Worker sprite:** Custom pixel art asset required. Commission or use a placeholder during dev?
- **Rain behavior:** Should the worker skip watering on rainy days (crops auto-watered), reducing that day's rate?
- **Zone overlap:** What happens if a zone rectangle includes tiles the worker cannot reach (water, cliffs)?
- **Tool animations:** Does the worker visually switch tools (axe for trees, watering can for crops), or is tool use abstracted?
- **Tree stumps:** Should stumps left by the player count as choppable objects for the cut trees task?
- **Worker entering buildings:** Worker tasks inside buildings (greenhouse, shed) require a location transition — needs separate handling from open-farm pathfinding. `Utility.ForAllLocations` can enumerate building interiors; worker teleport or warp to building entrance is likely the right approach.
- **Multiplayer:** Out of scope for v1. Behavior in multiplayer sessions is undefined.

---

## Out of scope — v1

- Multiple simultaneous workers
- Worker skill levels or leveling system
- Named or persistent worker characters
- Worker inventory or tool management
- Item filtering or sorting by task
- Fruit tree felling (hard excluded)
- Multiplayer support
- Tilling / planting tasks
- Fishing
- Mine / dungeon work
