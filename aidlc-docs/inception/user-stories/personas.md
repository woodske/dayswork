# Personas — Dayswork

Three personas were chosen during planning (see [story-generation-plan.md](../plans/story-generation-plan.md)): one unified human player, the worker NPC modelled as a system actor, and the mod maintainer for testability/translation framing.

---

## P-01 — The Stardew Player

> *"I love my farm, but I don't want to spend half a season just refilling watering cans."*

| Attribute | Detail |
|---|---|
| **Persona type** | Primary, human, unified across playstyles |
| **Context** | Single-player Stardew Valley 1.6 save. Mid-to-late game (has at least Year 1 Summer behind them; owns an axe and a pickaxe at some upgrade level). Plays on PC, Steam Deck, or controller. Uses the bulletin board for the existing quests system, so the entry point is already familiar. |
| **Motivations** | Free up real-world play time to pursue non-farm activities (mines, town events, fishing, community-center bundles, friendships). Wants the farm to feel cared-for even on days when the player is doing other things. |
| **Pain points** | Watering 80 crop tiles every morning eats 15+ real minutes. Harvesting on a Fall 28 strawberry day is exhausting. Late-game farms get overwhelming. Upfront-deposit-then-refund pricing feels fiddly and makes recurring hiring harder to trust. |
| **Goals when using Dayswork** | (1) Hand off the boring chores. (2) See visible, satisfying work happening. (3) Never lose items. (4) Predictable, fair economics with a simple fixed daily price. (5) Understand how much work the farmhand can still do from a visible energy bar. |
| **Tech comfort** | Comfortable installing SMAPI mods from Nexus. May read the README; rarely reads code. May use gamepad. May not speak English natively. |
| **Stories they care about** | Most of the player-facing journey: discovery, hiring, configuration, schedule, day-of-work, mail, edits, edge cases. Specifically all of S-01 through S-15 and S-17, S-18. |

---

## P-02 — The Farmhand

> *"I show up at six, I do the work, I bring the eggs to the chest, I go home. If I get fenced in, I figure it out."*

| Attribute | Detail |
|---|---|
| **Persona type** | System actor (the worker NPC). Not a real human — modeled as a persona so worker-AI behavior reads naturally as story-driven rather than purely behavioral spec. |
| **Context** | A generic, unnamed hired NPC. Spawns at the farm entrance at 6am on contract days. Operates with a one-shot capability snapshot of the player's tool levels (FR-TOOL-01). Invulnerable to player weapons (FR-NPC-02). Constrained to outdoor tiles + warps into buildings (FR-WORK-09). Works from a visible daily energy budget that generally mirrors player labor effort. |
| **Motivations (anthropomorphized)** | Complete assigned tasks within capability. Spend daily energy on useful work. Deposit collected items safely. Don't drop anything. Get home before 8pm. |
| **Pain points** | Pathfinding into geometry it can't escape (fences, walls, furniture). Full chests. Chest disappears mid-shift (building demolished). Festival days that consume the whole day. Player swinging a sword at it. |
| **Goals encoded as behavior** | Priority-ordered task execution (FR-WORK-03). Capability-based silent skipping (FR-SKIP-01/02). Hybrid stuck escalation (FR-WORK-12). Atomic shift completion on early player sleep (FR-DAY-02). Finish the current work unit when energy reaches zero, then deposit and leave (FR-WORK-06/17). |
| **Stories they care about** | All worker-perspective stories: S-07 (arrival), S-08 (priority order), S-09 (capability snapshot), S-10 (deposit), S-15 (sleep fast-forward), S-16 (stuck recovery), S-17 (invulnerable). |

---

## P-03 — The Mod Maintainer

> *"I want to ship a fix without spinning up Stardew, attaching a debugger, and walking my farmer to the bulletin board to test."*

| Attribute | Detail |
|---|---|
| **Persona type** | Developer / operator. The solo author (Bindicle) at v1, plus any future open-source contributors after the MIT release on Nexus. |
| **Context** | Experienced software engineer. New to C# and SMAPI (per NFR-ONBOARD-02). Uses Visual Studio 2026. Targets .NET 6 / SMAPI 4.x / SV 1.6.x. Adopts xUnit + FsCheck for testing under Partial-mode PBT enforcement. |
| **Motivations** | Ship a quality MIT-licensed Nexus mod with low ongoing support burden. Allow future contributors to make changes confidently. Keep upgrade-day painless when SMAPI or SV releases a new version. |
| **Pain points** | Stardew tightly couples business logic with the game runtime. Without separation, every test requires launching the game. Localization that lives in code can't be updated by the community without a PR. |
| **Goals** | (1) Pure business logic (contract pricing, energy accounting, zone-tile intersection, save-data DTOs) is isolated and unit-testable without the game. (2) All user-visible strings are routed through SMAPI's i18n so translators contribute without touching C#. |
| **Stories they care about** | S-19 (testable pure logic), S-20 (i18n routing). |

---

## Persona → Story Coverage Matrix

| Story group | P-01 Player | P-02 Farmhand | P-03 Maintainer |
|---|:-:|:-:|:-:|
| Discovery & first hire (S-01 to S-06) | ✓ | | |
| First day of work (S-07 to S-11) | ✓ | ✓ | |
| Daily life with recurring contracts (S-12 to S-13) | ✓ | | |
| Calendar & edge cases (S-14 to S-18) | ✓ | ✓ | |
| Maintainability (S-19 to S-20) | | | ✓ |
