# Dayswork — Requirements

**Source documents**: [source-spec.md](../source-spec.md) (user-provided design draft) + [requirement-verification-questions.md](requirement-verification-questions.md) (29 answered clarifying questions).

---

## 1. Intent Analysis Summary

| Field | Value |
|---|---|
| **Request type** | New project (greenfield) |
| **Scope estimate** | System-wide — UI menus, NPC, pathfinding, persistence, payments, mail, configuration |
| **Complexity estimate** | Complex — multi-component, save-game side effects, in-game currency + items at stake, NPC AI |
| **User profile** | Experienced software engineer; **new to C# and SMAPI** (onboarding-level docs are in-scope for this project, just-in-time during Construction per Q5) |
| **Requirements depth** | Comprehensive |
| **One-line summary** | A Stardew Valley 1.6 SMAPI mod that lets the player hire a generic farmhand NPC from the bulletin board to perform configurable farm tasks within configurable zones, paid via an upfront refundable deposit. |

---

## 2. Functional Requirements

> Each FR has an ID, a one-line rule, and a "Source" pointer (spec section or Q-number).

### 2.1 Hiring entry point and menu
| ID | Requirement | Source |
|---|---|---|
| FR-HIRE-01 | The Pelican Town bulletin board displays a "Hire a Farmhand" option, added via a Harmony patch to the bulletin-board menu. | spec §Hiring menu |
| FR-HIRE-02 | Selecting the option opens a custom 4-screen hiring UI: (1) Task selection, (2) Zone & output configuration, (3) Schedule, (4) Summary & confirm. | spec §Hiring menu |
| FR-HIRE-03 | The hiring UI is fully navigable with mouse/keyboard **and** gamepad. | Q24 |
| FR-HIRE-04 | Screen 1 displays all available tasks as toggles; the hourly rate updates live as tasks are enabled/disabled; the base rate is always charged; each task shows its rate contribution. | spec §Screen 1 |
| FR-HIRE-05 | Screen 2 lets the player draw tile-rectangle zones (click-and-drag on the farm map) and select buildings (click). Multiple zones and multiple buildings can be combined in one contract. | spec §Screen 2 |
| FR-HIRE-06 | For each task that produces output, the player can assign an output destination. Open-farm chests are assigned by clicking the chest on the farm map (same interaction as zone tile selection). Building chests are assigned via a dropdown panel grouped by building. | spec §Screen 2 |
| FR-HIRE-07 | Building-chest labels use the in-game chest name if set; otherwise fall back to "{Building name} — Chest at {x}, {y}". Buildings with no chests are omitted from the dropdown. | spec §Screen 2 |
| FR-HIRE-08 | A chest assignment is identified by location name + tile coordinates. Renaming the chest does not break the assignment; moving it does. | spec §Screen 2 |
| FR-HIRE-09 | Multiple tasks can share the same output chest. | spec §Screen 2 |
| FR-HIRE-10 | If no chest is assigned to a task that produces output, the worker buffers the output and mails it to the player the following morning (no penalty). | spec §Screen 2 |
| FR-HIRE-11 | Screen 3 offers two schedules: **one-time** (next morning only) and **recurring** (each morning automatically, daily deposit). | spec §Screen 3 |
| FR-HIRE-12 | Recurring contracts can be paused or cancelled from the bulletin board any time before 6am. Zones and task settings persist between days and are editable. | spec §Screen 3 |
| FR-HIRE-13 | Screen 4 shows: selected tasks, estimated hours, hourly rate, total deposit, refund policy. Confirming deducts the deposit immediately. | spec §Screen 4 |
| FR-HIRE-14 | If the player cannot afford the deposit at confirmation, confirmation is blocked with a clear error message. | spec §Screen 4 |
| FR-HIRE-15 | Once a contract has started for the day, the player cannot cancel mid-shift. The shift runs until all tasks complete or the 8pm hard cap. | Q25 |

### 2.2 Tasks (v1 scope)
| ID | Requirement | Source |
|---|---|---|
| FR-TASK-01 | The v1 task set is: Water crops, Harvest crops, Collect fruit, Feed animals, Pet animals, Collect animal products, Cut trees, Clear rocks, Clear weeds, Clear grass. | spec §Tasks |
| FR-TASK-02 | Harvest crops, Collect fruit, and Collect animal products are configured per-task to deposit either into the shipping bin or a designated chest (player choice in Screen 2). | spec §Tasks + change request |
| FR-TASK-03 | Feed animals consumes hay from the silo and produces no output. | spec §Tasks |
| FR-TASK-04 | Collect animal products gathers eggs, milk, wool, truffles, etc. The destination is selected per FR-TASK-02 (shipping bin or designated chest). | spec §Tasks + change request |
| FR-TASK-05 | Cut trees deposits wood, sap, and seeds into the designated chest. **Fruit trees are always excluded from felling**, regardless of zone or axe level — hard rule, no toggle in v1. | spec §Tasks (callout) |
| FR-TASK-06 | When "Cut trees" is enabled, the worker also chops standing player-left stumps (the small stumps left after the player chops a tree). | Q13 |
| FR-TASK-07 | Clear rocks deposits stone, ore, geodes, and gems into the designated chest — **no item filtering** in v1 (all rock-clearing drops go to the same chest). | spec §Tasks, §No item filtering |
| FR-TASK-08 | Clear weeds deposits fiber and mixed seeds into the designated chest. | spec §Tasks |
| FR-TASK-09 | Clear grass attempts to store hay in the silo first (matching vanilla scythe behavior). If the silo is full, hay is dropped on the ground at the worker's current tile. If the player has no silo, grass is cleared but no hay is produced. Hay is **never mailed** (prevents item duplication via mail). | spec §Hay |

### 2.3 Worker arrival, departure, and shift loop
| ID | Requirement | Source |
|---|---|---|
| FR-WORK-01 | The worker spawns at the farm entrance at 6am on contract days. | spec §Arrival |
| FR-WORK-02 | The worker pathfinds between task tiles using the game's built-in `PathFindController`. | spec §Tech |
| FR-WORK-03 | Within a zone, tasks execute in this priority order: Feed animals → Pet animals → Collect animal products → Water crops → Harvest crops → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees. | spec §Task priority |
| FR-WORK-04 | The shift ends when all tasks in all zones are complete, **or** at the 8pm hard cap. | spec §Arrival |
| FR-WORK-05 | After shift end, the worker walks to each unique designated chest (one trip per chest) and deposits all buffered items. Items from multiple tasks going to the same chest are deposited in one trip. | spec §Arrival, §Deposit |
| FR-WORK-06 | If the 8pm cap is reached mid-task, the worker still completes all deposit runs before leaving — items are never lost in v1. | spec §Arrival |
| FR-WORK-07 | The worker exits via the farm entrance after all deposits complete. Refund is calculated and added to player gold at the moment of exit. | spec §Arrival |
| FR-WORK-08 | Unreachable tiles inside a zone (water, cliffs, walls) are silently skipped by the worker and do not count toward estimated hours. No UI warning at hire time. | Q11 |
| FR-WORK-09 | The worker enters buildings by walking to the building door and warping inside (vanilla NPC pattern). | Q14 |
| FR-WORK-10 | The worker visually swaps tools (axe / watering can / scythe / pickaxe) when changing task types. (Sprite work scoped accordingly.) | Q12 |
| FR-WORK-11 | The worker is considered **stuck** if it makes no progress toward its current target tile and completes no task work for a configurable in-game-minutes threshold (default: 10 in-game minutes). Progress is measured in tile movement or completed task ticks. | Change request (stuck handling) |
| FR-WORK-12 | On stuck detection, the worker escalates through three steps: (1) play a confused emote (e.g., "?" speech balloon), (2) attempt to teleport to the next reachable task tile in the priority queue and resume work, (3) if still stuck after another stuck-detection window, teleport to the farm entrance and end the shift early as if the 8pm cap had been reached. Buffered items are deposited into assigned chests where reachable, otherwise mailed next morning per FR-OUT-02/03/04/05. Refund is computed from actual hours worked per FR-PAY-05. | Change request (stuck handling — option C, hybrid escalation) |
| FR-WORK-13 | Stuck-detection thresholds (initial wait and post-teleport wait) are configurable via GMCM. | Change request |

### 2.4 Skipped objects (capability-based)
| ID | Requirement | Source |
|---|---|---|
| FR-SKIP-01 | The worker silently skips stumps/logs it cannot chop given the player's axe upgrade level at spawn. | spec §Skipped |
| FR-SKIP-02 | The worker silently skips boulders it cannot break given the player's pickaxe upgrade level at spawn. | spec §Skipped |
| FR-SKIP-03 | Fruit trees are always skipped by the cut-trees task regardless of axe level or zone. | spec §Skipped (hard rule) |
| FR-SKIP-04 | Trellis crops are harvested from adjacent reachable tiles; if surrounded with no reachable adjacent tile, the worker skips them. | spec §Skipped |
| FR-SKIP-05 | Crops not yet ready to harvest are skipped. | spec §Skipped |

### 2.5 Tool inheritance
| ID | Requirement | Source |
|---|---|---|
| FR-TOOL-01 | At 6am spawn, the mod reads the upgrade level of each of the player's tools and stores a snapshot in the worker's state. The snapshot is locked for the shift; tool changes mid-day do not affect the worker. | spec §Tool inheritance |
| FR-TOOL-02 | Worker capability per tool level matches the spec's Tool-inheritance table (Axe, Pickaxe, Watering Can, Hoe). The Hoe is unused in v1. | spec §Tool inheritance |
| FR-TOOL-03 | If the player does not own a tool (e.g., sold it), the snapshot treats its level as 0 and the worker skips all tasks requiring that tool. A mail warning is sent the following morning. | spec §Tool inheritance |
| FR-TOOL-04 | The worker never holds or consumes the player's tools; the upgrade level is a read-only capability check. | spec §Tool inheritance |

### 2.6 Output, deposit, fallback
| ID | Requirement | Source |
|---|---|---|
| FR-OUT-01 | The worker holds all collected items in an internal buffer for the entire shift. | spec §Deposit |
| FR-OUT-02 | Chest full: the worker deposits what fits and buffers the overflow. Buffered overflow is mailed the following morning. | spec §Chest full or missing |
| FR-OUT-03 | Chest removed mid-shift (chest destroyed, building demolished): the worker silently buffers all items for that task; everything mails the next morning. The worker continues other tasks. | Q19 |
| FR-OUT-04 | If no chest is assigned for a task that produces output, the worker still performs the task and buffers all drops; everything mails the next morning. | spec §Chest full or missing |
| FR-OUT-05 | All overflow mail arrives from "Your farmhand" with a brief note and the items attached. No fee, no penalty. | spec §Chest full or missing |
| FR-OUT-06 | Shipping bin has no capacity limit (vanilla); overflow cannot occur when the shipping bin is the destination. | spec §Chest full or missing |
| FR-OUT-07 | Within a task, all drops go to a single designated chest with no sorting or filtering. | spec §No item filtering |

### 2.7 Pricing, deposit, refund
| ID | Requirement | Source |
|---|---|---|
| FR-PAY-01 | Base hourly rate is 50g, always charged. Each enabled task adds a configurable per-task increment to the hourly rate (see spec table for defaults). | spec §Pricing |
| FR-PAY-02 | Deposit = hourly rate × estimated hours. Estimated hours are derived from zone size, number of tasks, and a configurable "average speed" constant. | spec §Deposit |
| FR-PAY-03 | One-time contracts deduct the deposit immediately at confirmation. Recurring contracts deduct the daily deposit at 6am on each contract day. | spec §Deposit |
| FR-PAY-04 | If the player cannot afford the daily deposit on a recurring contract, the worker does not show up that day and a mail notification is sent. | spec §Deposit |
| FR-PAY-05 | Unused deposit is refunded at shift end: `refund = deposit − (actual hours worked × hourly rate)`. Refund is added directly to player gold at worker exit. Deposit-run time (walking to chests post-shift) is **not** billed. | spec §Deposit |
| FR-PAY-06 | If a day's selected work yields zero actionable objects (empty zone), the player is fully refunded (effectively no charge beyond the base rate × 0 hours = 0). | Q20 |
| FR-PAY-07 | On rainy days, if Water Crops is enabled, the watering task is skipped and that day's hourly rate is recalculated to exclude the Water Crops surcharge. The worker still shows up if any other task is enabled. | Q10 |
| FR-PAY-08 | Config-driven rate changes (via GMCM) for active recurring contracts apply starting the next morning. The current day's deposit and refund are at the rate in effect when that day began. | Q21 |
| FR-PAY-09 | All rates are configurable via GenericModConfigMenu (GMCM) and `config.json`. | spec §Pricing |

### 2.8 Day & calendar edge cases
| ID | Requirement | Source |
|---|---|---|
| FR-DAY-01 | On festival days, the worker does not show up. For recurring contracts, the daily deposit is not deducted. No mail is sent. | Q16 |
| FR-DAY-02 | If the player goes to sleep before the worker's shift ends, the worker completes the rest of the shift off-screen instantly at sleep-confirm. The deposit run is performed atomically, the refund is applied, and overflow mail (if any) is queued for next morning before the day rolls over. | Q17 |

### 2.9 Worker NPC behavior
| ID | Requirement | Source |
|---|---|---|
| FR-NPC-01 | The worker uses a placeholder sprite for v1 (recolored vanilla NPC). Custom art is post-v1 scope. | Q9 |
| FR-NPC-02 | The worker is invulnerable to player weapon swings. On hit, the worker plays a brief "ouch" / surprised animation + emote but takes no damage and does not abandon the shift. | Q18 |

### 2.10 Persistence and lifecycle
| ID | Requirement | Source |
|---|---|---|
| FR-PERSIST-01 | Contracts, zones, chest assignments, and recurring schedule state persist per save file via the SMAPI save data API. | spec §Tech |
| FR-PERSIST-02 | No special cleanup is performed when the mod is uninstalled. Leaked save segments are harmless. | Q22 |

### 2.11 Multiplayer
| ID | Requirement | Source |
|---|---|---|
| FR-MP-01 | The mod refuses to load (or no-ops the bulletin patch) in multiplayer sessions and logs a friendly SMAPI warning. v1 is single-player only. | Q15 |

### 2.12 Configuration & UX
| ID | Requirement | Source |
|---|---|---|
| FR-CFG-01 | All player-tunable values (base rate, per-task rates, average-speed constant, 8pm cap, etc.) are exposed via GMCM with sensible defaults matching the spec. | spec §Pricing, §Tech |
| FR-CFG-02 | All user-visible strings are routed through SMAPI's i18n system (`i18n/default.json`), so community translators can add languages without code changes. v1 ships English only. | Q23 |

### 2.13 Mod compatibility
| ID | Requirement | Source |
|---|---|---|
| FR-COMPAT-01 | Known-conflicting mods (Automate, Junimo Helper, other farmhand mods) are documented in the README. No runtime detection in v1. | Q26 |
| FR-COMPAT-02 | All seven vanilla farm types are officially supported. The Standard Farm is the minimum testbed; modded custom farms are best-effort. | Q27 |

---

## 3. Non-Functional Requirements

### 3.1 Compatibility & platform
| ID | Requirement |
|---|---|
| NFR-COMPAT-01 | Targets Stardew Valley 1.6.x and SMAPI 4.x on .NET 6. (Q2) |
| NFR-COMPAT-02 | No new buildings are required; relies on the existing Pelican Town bulletin board as the entry point. (Spec §Design goals) |
| NFR-COMPAT-03 | Single-player only. (FR-MP-01) |
| NFR-COMPAT-04 | Optional dependency: GenericModConfigMenu (GMCM). Required dependency: Harmony (bundled with SMAPI). |

### 3.2 Safety & data integrity
| ID | Requirement |
|---|---|
| NFR-SAFE-01 | No items are ever lost: every drop the worker collects is either deposited into a chest/shipping bin, buffered for next-day mail, or (for hay only) dropped on the ground per vanilla scythe behavior. |
| NFR-SAFE-02 | No gold is ever lost beyond the contractually-billed hourly rate × hours worked. Refunds are integer-clamped to avoid floating-point gold leakage. |
| NFR-SAFE-03 | The mod must not corrupt save files. All persisted data is namespaced via SMAPI's data API and tolerates being absent on first load. |
| NFR-SAFE-04 | The worker never picks up items the player has dropped or placed; it only collects drops it caused. |

### 3.3 Performance
| ID | Requirement |
|---|---|
| NFR-PERF-01 | The worker's per-frame update must not introduce visible frame drops on typical hardware. (Stardew targets 60fps; the worker's update loop should stay well under 1ms per frame.) |
| NFR-PERF-02 | Tile scanning to build the task queue happens once at zone entry per shift, not per frame. |
| NFR-PERF-03 | The hiring UI's zone overlay rendering must remain responsive for zones up to the size of the full Standard Farm map (~80×65 tiles). |

### 3.4 Usability
| ID | Requirement |
|---|---|
| NFR-UX-01 | Full gamepad navigation for all hiring UI screens. (Q24) |
| NFR-UX-02 | All user-visible strings localizable via i18n/default.json. (Q23) |
| NFR-UX-03 | Hiring UI does not require the player to leave the bulletin board to configure zones — zone draw mode overlays the farm map and returns to Screen 2 on completion. |

### 3.5 Maintainability & testability
| ID | Requirement |
|---|---|
| NFR-MAINT-01 | Test framework: **xUnit**. (Q4) Unit tests live in a separate project (e.g., `Dayswork.Tests`) referencing the main mod assembly. |
| NFR-MAINT-02 | Property-based testing framework: **FsCheck** (xUnit integration). (PBT-09) PBT enforcement is **Partial mode**: rules PBT-02, PBT-03, PBT-07, PBT-08, PBT-09 are blocking; PBT-01, PBT-04, PBT-05, PBT-06, PBT-10 are advisory. |
| NFR-MAINT-03 | Pure business-logic modules (rate calculation, deposit/refund math, tile-zone intersection, save-data DTO round-trips) are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew. |
| NFR-MAINT-04 | Harmony patches are isolated in a single namespace (e.g., `Dayswork.Patches`) for visibility and conflict diagnosis. |
| NFR-MAINT-05 | Code style follows standard .NET conventions (`dotnet format`); CI configuration (optional v1) enforces format check + test execution. |

### 3.6 Onboarding (cross-cutting; affects docs more than code)
| ID | Requirement |
|---|---|
| NFR-ONBOARD-01 | C# / SMAPI / Harmony concepts are explained **just-in-time** during Construction stages, embedded in Code Generation plans rather than front-loaded. (Q5) |
| NFR-ONBOARD-02 | The README and Construction docs assume the reader is an experienced engineer in other languages but new to C# / .NET / Stardew modding. Idioms unique to C# (e.g., events, async, properties) and SMAPI (e.g., `IModHelper`, content packs, manifest format) are introduced in context. |

### 3.7 Licensing & distribution
| ID | Requirement |
|---|---|
| NFR-DIST-01 | Source code is published under the **MIT** license. (Q7) |
| NFR-DIST-02 | Release artifacts target **Nexus Mods**. (Q6) |
| NFR-DIST-03 | Mod manifest `Author` is **"Bindicle"**. (Q8) |

### 3.8 Security
| ID | Requirement |
|---|---|
| NFR-SEC-01 | Security Baseline extension is **disabled** for this project. (Q28) Rationale: no network surface, no PII, no auth, no external inputs. |

---

## 4. Out of Scope (v1)

Restated from spec §Out of scope, plus clarified items from Q&A:
- Multiple simultaneous workers
- Worker skill levels or leveling system
- Named or persistent worker characters (placeholder sprite, generic name)
- Worker inventory or tool management beyond capability snapshot
- Item filtering or sorting by task
- Fruit tree felling (hard excluded — FR-TASK-05)
- Multiplayer support (FR-MP-01)
- Tilling / planting tasks (Hoe unused)
- Fishing
- Mine / dungeon work
- Cancelling a shift mid-day (FR-HIRE-15)
- Runtime mod-conflict detection (FR-COMPAT-01 — docs only)
- Custom worker sprite art (Q9 — post-v1)
- Localizations beyond English at v1 launch (Q23 — i18n-ready but English-only)
- Save cleanup on uninstall (FR-PERSIST-02 — leaked segments are accepted)
- Overtime pay

---

## 5. Extension Configuration Summary

| Extension | Status | Notes |
|---|---|---|
| **Security Baseline** | Disabled | Q28: B. Rules tracked as N/A throughout the project; security review is not a blocking gate. |
| **Property-Based Testing** | Enabled — Partial mode | Q29: B. Blocking rules: **PBT-02** (round-trip), **PBT-03** (invariant), **PBT-07** (generator quality), **PBT-08** (shrinking/reproducibility), **PBT-09** (framework = FsCheck). Advisory: PBT-01, 04, 05, 06, 10. |

---

## 6. Key Decisions Captured

| Decision | Choice | Source |
|---|---|---|
| Game/SMAPI version | SV 1.6.x + SMAPI 4.x + .NET 6 | Q2 |
| IDE for development | Visual Studio 2026 | Q3 (revised) |
| Test framework | xUnit + FsCheck | Q4, Q29 |
| Onboarding strategy | Just-in-time during Construction | Q5 |
| Distribution | Nexus Mods | Q6 |
| License | MIT | Q7 |
| Author handle | Bindicle | Q8 |
| Worker sprite | Recolored vanilla placeholder for v1 | Q9 |
| Rain handling | Skip watering, reduce rate | Q10 |
| Unreachable tiles | Silent skip | Q11 |
| Tool animations | Visible swap | Q12 |
| Player stumps | Choppable | Q13 |
| Building entry | Walk to door + warp (vanilla) | Q14 |
| Multiplayer | Refuse to load | Q15 |
| Festival days | Worker stays home | Q16 |
| Early sleep | Fast-forward shift atomically | Q17 |
| Player attacks worker | Invulnerable, plays "ouch" reaction | Q18 |
| Chest gone mid-shift | Buffer + mail next morning | Q19 |
| Empty zone | Full refund, no charge | Q20 |
| Mid-contract rate changes | New rates apply next morning | Q21 |
| Uninstall cleanup | None | Q22 |
| Localization | i18n-ready, English at launch | Q23 |
| Gamepad UI | Full support | Q24 |
| Cancel mid-shift | Not supported | Q25 |
| Mod conflict detection | Docs only | Q26 |
| Farm map support | All 7 vanilla farms official | Q27 |
| Stuck handling | Hybrid escalation: emote → teleport to next task tile → if still stuck, teleport home and end shift early | Change request |

---

## 7. Brief Summary

**Dayswork** is a single-player Stardew Valley 1.6 mod, built in C# / .NET 6 against SMAPI 4.x, that adds a "Hire a Farmhand" option to the Pelican Town bulletin board. The player configures tasks, zones, and output chests through a four-screen UI (full gamepad support). A generic NPC walks onto the farm at 6am, performs prioritized tasks within tool-capability limits, deposits collected items into designated chests (or mails overflow), and exits via the entrance — applying a refund of unused deposit. The mod is engineered around a "no items, no gold are ever lost" safety invariant: all overflow becomes next-morning mail, and refunds are pro-rated against actual hours worked. v1 is single-player only, uses a placeholder worker sprite, is published under MIT to Nexus Mods under author "Bindicle", and is i18n-ready (English-only at launch). Pure business logic (rates, deposits, zone math, save round-trips) is tested with xUnit + FsCheck under Partial-mode PBT enforcement.
