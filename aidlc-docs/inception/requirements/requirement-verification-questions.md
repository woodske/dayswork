# Requirements Verification Questions — Dayswork

**How to use this file**:
- Each question has letter options (A, B, C, …) and an `[Answer]:` line.
- Fill in the letter (or `X` for *Other*, with your custom description on the same line).
- When all answers are filled, reply with "done" (or "completed" / "finished") and I will analyze responses, ask follow-ups if anything is contradictory or ambiguous, and then generate `requirements.md`.

> **Reading note**: Questions are grouped. Skim group headers first; many groups have a "use the spec defaults" option so you can move fast where the spec is already clear.

---

## Intent Summary (for confirmation)

I've parsed `aidlc-docs/inception/source-spec.md` as:
- **Request type**: New project (greenfield SMAPI mod)
- **Scope**: System-wide — UI menus, NPC, pathfinding, task execution, persistence, payments, mail fallback, config
- **Complexity**: Complex (multi-component, save-game side effects, money/items at stake, NPC AI)
- **User profile**: Experienced engineer, **new to C# and SMAPI** → onboarding-level tech stack rationale is required

## Question 1
Is the intent summary above accurate, or am I missing something fundamental about the project?

A) Accurate — proceed
B) Mostly accurate, but I'll note corrections in the [Answer] line below
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

# Group A — Tech stack & developer experience (you flagged this as in-scope)

## Question 2
Stardew Valley + SMAPI versions to target. Stardew 1.6 ships big NPC/API improvements over 1.5; SMAPI 4.x requires .NET 6.

A) Latest stable Stardew Valley 1.6.x + SMAPI 4.x + .NET 6 (recommended — this is where the modding community is)
B) Stardew Valley 1.5.x + SMAPI 3.x (legacy — only choose if you're locked to an older install)
C) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
Preferred IDE for C# development. This affects which scaffolding/instructions I produce.

A) Visual Studio 2022 (Community is free; full debugger + designer; Windows-friendly)
B) JetBrains Rider (paid, but excellent Unity/.NET experience; cross-platform)
C) Visual Studio Code with C# Dev Kit (lightest weight; you may already have it)
D) Other (please describe after [Answer]: tag below)

[Answer]: A — revised to Visual Studio 2026 (see audit.md 2026-05-18T00:00:08Z)

## Question 4
Test framework preference. SMAPI mods are typically tested with a thin unit-test project that targets the same .NET 6 and references your mod assembly.

A) xUnit (most popular in modern .NET; recommended)
B) NUnit
C) MSTest
D) Skip automated tests for v1 — manual testing only
E) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
How much "onboarding" content do you want bundled into the docs?

A) Full — explain SMAPI lifecycle, Harmony patching basics, content packs, debugging, mod-publishing workflow (longer Inception, faster Construction)
B) Targeted — only explain things as we hit them in Construction (shorter docs, more "just-in-time" learning)
C) Minimal — assume I'll Google what I don't know; just give me the code
D) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 6
Distribution target — where will the mod be published?

A) Nexus Mods only (standard for Stardew)
B) Nexus Mods + ModDrop
C) GitHub releases only (for now)
D) Not deciding yet — set up GitHub repo and revisit later
E) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 7
License for the mod source code. (Required for publishing; affects whether others can fork/redistribute.)

A) MIT (permissive — most common for Stardew mods)
B) GPL-3.0 (copyleft — derivative mods must also be GPL)
C) Apache-2.0 (permissive + explicit patent grant)
D) All rights reserved / no license (you decide later)
E) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 8
Author handle on the mod manifest. My memory says you've used **"Bindicle"** for Stardew modding previously.

A) Use "Bindicle"
B) Use a different handle (specify after [Answer]: tag below)
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

# Group B — Resolving the spec's "Open questions" section

These are the open questions you flagged in the spec itself. Picking now lets us close them out before User Stories.

## Question 9
Worker sprite — what's the plan for v1?

A) Use a placeholder sprite (recolored vanilla NPC) for now; commission custom art post-v1
B) Use a placeholder sprite indefinitely (functional > pretty)
C) I'll commission/draw custom art in parallel; design code as if final sprite is ready
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 10
Rain behavior — crops auto-water when it rains.

A) Worker still shows up but skips watering; daily rate is recalculated to exclude the Water Crops surcharge
B) Worker shows up and waters anyway (no-ops on already-watered tiles); full rate charged
C) Worker doesn't show up at all on rainy days if Water Crops is the only enabled task; full refund
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 11
Zone overlap with unreachable tiles (water, cliffs, walls).

A) Worker silently skips unreachable tiles; they don't count against estimated hours
B) Worker silently skips, but during hiring the UI shows a warning if >X% of selected zone is unreachable
C) Hiring UI rejects the zone if any selected tile is unreachable (player must redraw)
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 12
Tool animations — does the worker visually swap tools?

A) Visible tool swap (axe → can → scythe) — most immersive, more sprite work and code complexity
B) Single generic "working" animation regardless of task — simpler, ships v1 faster
C) No animation at all — worker just stands on tile, item appears/disappears (placeholder for v1)
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 13
Tree stumps left by the player (stumps from already-chopped trees) — should the worker chop those when "Cut trees" is enabled?

A) Yes — stumps count as choppable objects
B) No — stumps are ignored (worker only chops standing trees)
C) Configurable in GMCM (default: yes)
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 14
Worker entering buildings (greenhouse, shed, barn, coop).

A) Worker walks to the building door, then warps inside (vanilla NPC behavior — simplest, recommended)
B) Worker walks to the door, plays an "entering" animation, warps inside
C) Skip building interiors for v1 — only outdoor tiles supported
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 15
Multiplayer behavior — currently "undefined" in spec.

A) Mod refuses to load in multiplayer sessions (with a friendly SMAPI log message) — safest for v1
B) Mod loads but disables the bulletin-board option in multiplayer
C) Mod loads and best-effort works; multiplayer bugs are accepted as known limitations
D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

# Group C — Critical edge cases not covered in the spec

These came up reviewing the spec and are likely to bite us in Construction if undecided.

## Question 16
Festival days (Egg Festival, Flower Dance, etc.) — these "consume" the day; player is transported to the festival location.

A) Worker does not show up on festival days; recurring deposit not deducted; no mail
B) Worker shows up before the festival starts (6am → ~9am), works the limited window, refund based on hours worked
C) Worker shows up, but the contract is paused for the day if festival start time is reached mid-shift; refund applied
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 17
Player sleeps before worker finishes the shift. (Sleeping advances time to 6am next day.)

A) Sleeping is blocked with a dialog: "Your farmhand is still working." until shift ends (or 8pm cap)
B) Sleeping is allowed; worker completes their shift "off-screen" instantly; deposit run + refund happen at sleep-confirm
C) Sleeping is allowed; worker is cut short at sleep time; remaining buffered items mailed next morning; refund based on hours actually worked
D) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 18
Player attacks the worker with a weapon swing (intentional or accidental).

A) Worker is invulnerable to player weapons (no damage, no reaction)
B) Worker is invulnerable but plays a brief "ouch" / surprised animation + emote
C) Worker takes damage and abandons shift if defeated; deposit forfeit for remaining hours
D) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 19
Assigned chest gets demolished / building containing it is destroyed mid-shift.

A) Worker silently buffers all items for that task; everything mails next morning (consistent with "no items lost")
B) Worker stops the contributing task, but continues other tasks; mail next morning
C) Worker abandons entire shift, mails everything, refund based on hours worked so far
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 20
Empty zone or zero work to do (e.g., no rocks in the rock-clearing zone today).

A) Full refund (only base rate charged for 0 hours, so effectively no charge)
B) Charge minimum 1-hour base rate as a "show-up fee"
C) Worker doesn't show up; deposit refunded in full + mail notice "Nothing to do today"
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 21
Config rate changes for an active recurring contract (player edits hourly rates in GMCM while a contract is running).

A) New rates apply starting the next morning (existing day's deposit at old rate)
B) New rates apply immediately; remainder of current day prorated
C) Config rate changes do not affect existing contracts — only new ones use new rates
D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 22
Mod is uninstalled mid-contract — should we attempt any graceful save-file cleanup?

A) No special handling — SMAPI's own data API will leak our save segment, but it's harmless
B) Provide a "Cancel all contracts" admin command (SMAPI console) the player can run before uninstalling
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

# Group D — UX & scope choices

## Question 23
Localization support for v1.

A) English only — hardcoded strings (fastest)
B) English only, but route all strings through a `i18n/default.json` so community translators can add languages without code changes (recommended SMAPI pattern; small upfront cost)
C) Ship with English + at least one other language at launch (specify which after [Answer]: tag)
D) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 24
Controller / gamepad support for the hiring UI.

A) Yes — all four screens fully gamepad-navigable (Stardew's audience uses controllers heavily; recommended)
B) Mouse/keyboard only for v1; gamepad later
C) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 25
Can the player cancel a worker's shift early (e.g., "you can go home now") while the worker is on the farm?

A) Yes — cancel button on the bulletin board sends worker home immediately; refund based on hours worked
B) No — once the day starts, the shift runs until tasks done or 8pm
C) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 26
Mod compatibility — should we explicitly check for and warn about known conflicting mods (e.g., Automate, Junimo Helper, other farmhand mods)?

A) Yes — at load time, scan loaded mods and log warnings for known overlaps; document conflicts in README
B) Just document conflicts in README; no runtime check
C) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 27
Custom farm maps (Forest Farm, Beach Farm, Hilltop, Stardew Valley Expanded's custom farms).

A) Officially support all 7 vanilla farm types; test on at least the standard farm; community can report custom-farm issues
B) Officially support standard farm only; explicitly state others are "best effort, no guarantees"
C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

# Group E — Mandatory extension opt-ins

## Question 28: Security Extensions
Should security extension rules be enforced for this project?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended for production-grade applications)
B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental projects)
X) Other (please describe after [Answer]: tag below)

[Answer]: B

> *Context for your decision*: A Stardew mod has no network surface, no PII, no auth. The security baseline rules are largely **N/A** for this codebase. Recommend **B** unless you want the rules' general hygiene reviews regardless.

## Question 29: Property-Based Testing Extension
Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects with business logic, data transformations, serialization, or stateful components)
B) Partial — enforce PBT rules only for pure functions and serialization round-trips (suitable for projects with limited algorithmic complexity)
C) No — skip all PBT rules (suitable for simple CRUD applications, UI-only projects, or thin integration layers with no significant business logic)
X) Other (please describe after [Answer]: tag below)

[Answer]: B

> *Context for your decision*: Dayswork has real pure-function business logic worth PBT-ing: hourly-rate calculation, deposit/refund math, item-buffer overflow, tile-zone intersection, save-data round-trips. The NPC AI and Harmony patches are not good PBT targets. Recommend **B** (partial).

---

# Done? 

When all 29 answers are filled, reply with "done" and I'll:
1. Validate every `[Answer]:` line has a choice.
2. Check for contradictions (e.g., "no automated tests" + "full PBT").
3. If contradictions found → write a clarification file.
4. Otherwise → write `aidlc-docs/inception/requirements/requirements.md` and ask for approval to proceed to **User Stories**.
