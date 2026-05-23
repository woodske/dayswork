# Integration Test Instructions — Dayswork SMAPI Mod

## Overview

Dayswork is a Stardew Valley SMAPI mod. There is no service mesh, no docker-compose, and no
API server to spin up. Integration testing is done by running the mod inside the actual game
with SMAPI and exercising end-to-end flows manually. SMAPI's console log is the primary
observation surface.

## Environment Setup

1. **Build and deploy** the mod (see `build-instructions.md`).
2. **Install required dependencies** in the game's `Mods/` folder:
   - Mail Framework Mod `>= 1.20.0`
3. **Optionally install** Generic Mod Config Menu `>= 1.14.0` to exercise the GMCM surface.
4. **Launch the game** via SMAPI (`StardewModdingAPI.exe`).
5. **Open a save** — use a farm with a mix of zones: crops, fruit trees, grass, rocks, animal buildings.

## Integration Test Scenarios

---

### Scenario 1: Mod Loads Without Errors

**Purpose**: Verify all integration seams (GMCM, MFM) initialise cleanly.

**Steps**:
1. Launch game with SMAPI.
2. Check SMAPI console for `[Dayswork]` log lines.

**Expected**:
- No `[ERROR]` or exception stack traces in SMAPI console.
- If GMCM is installed: `[Dayswork]` line confirming GMCM registration (or no crash).
- If GMCM is absent: mod still loads (optional dependency — silent no-op).

---

### Scenario 2: GMCM Config Screen

**Purpose**: Verify all config fields appear and save correctly.

**Steps**:
1. Open the in-game Options menu → Mod Options → Dayswork.
2. Observe all sections: **Rates** and **Worker Settings**.
3. Change `Base Rate` to a non-default value.
4. Close the menu (saves automatically).
5. Reopen the menu.

**Expected**:
- All fields show with correct labels and tooltips (from `i18n/default.json`).
- Changed value persists after reopen.
- No crash on save or reopen.

---

### Scenario 3: One-Time Contract — Hire, Work, Settle

**Purpose**: Verify the full one-time contract flow from hire to settlement mail.

**Steps**:
1. Go to the bulletin board on a non-festival weekday with enough gold.
2. Define a zone covering crops ready to harvest.
3. Hire a worker.
4. Progress time until the worker completes work.
5. Sleep.

**Expected**:
- Worker navigates to and harvests crops within the zone.
- Produces are deposited to the assigned chest (or overflow-mailed if chest full).
- Settlement mail arrives the next morning with any refund gold.
- No orphaned work items or crashes on sleep/save.

---

### Scenario 4: Recurring Contract — Multi-Day Deposit Deduction

**Purpose**: Verify recurring contracts deduct on each new day and skip festivals.

**Steps**:
1. Hire a worker on a recurring contract.
2. Advance one full day.
3. Advance to a festival day.

**Expected**:
- Day 1: Deposit deducted, work performed.
- Festival day: No deposit deducted, festival-skip mail arrives **same day** (in morning mailbox).

---

### Scenario 5: Animal Tasks (U-16)

**Purpose**: Verify per-animal pet+collect sequencing inside buildings.

**Steps**:
1. Assign the worker a zone that includes a barn or coop.
2. Ensure animals are ready to pet and have products to collect.
3. Watch the worker.

**Expected**:
- Worker enters the building.
- For each animal: pet the animal, then collect its product — before moving to the next animal.
- Products deposited to assigned chest, not dropped in player inventory.

---

### Scenario 6: Big Rock Multi-Hit (U-16)

**Purpose**: Verify boulders/large rocks take multiple hits and yield correct stone counts.

**Steps**:
1. Assign a zone containing a large boulder.
2. Observe the worker.

**Expected**:
- Worker strikes the boulder multiple times (not one-shot).
- Stone collected matches the rock's real drop count from game data.
- Rock is removed from the map after health reaches zero.

---

### Scenario 7: Greenhouse Crop Harvest (U-16)

**Purpose**: Verify greenhouse crops harvest correctly without an infinite loop.

**Steps**:
1. Assign a zone that includes the greenhouse.
2. Ensure at least one crop is harvestable.
3. Let the worker operate.

**Expected**:
- Worker harvests each ready crop exactly once.
- Crops go into the worker buffer (not the player's inventory).
- Regrowable crops remain (dirt cleared for non-regrowable crops).
- No repeated harvest invocations on the same tile.

---

## Checking SMAPI Logs

SMAPI writes a detailed log at:
```
%AppData%\StardewValley\ErrorLogs\SMAPI-latest.txt
```

Filter for `[Dayswork]` entries. Trace-level scan entries are logged at `Trace` verbosity
and are visible when SMAPI is launched with `--log-level Trace` or via `log level trace`
in the SMAPI console.

## Pass Criteria

| Scenario | Pass Condition |
|----------|---------------|
| 1 — Mod Loads | No errors in SMAPI console |
| 2 — GMCM Screen | All fields display; values persist |
| 3 — One-Time Contract | Work done; chest deposited; settlement mail correct |
| 4 — Recurring + Festival | Daily deduction works; festival skip mail same-day |
| 5 — Animal Sequencing | Pet+collect per animal before moving to next |
| 6 — Big Rock Multi-Hit | Multiple hits; correct stone yield |
| 7 — Greenhouse Harvest | Each tile harvested once; buffer not player inventory |
