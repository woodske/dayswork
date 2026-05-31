# SVE Compatibility — Integration / Manual Test Instructions

Scope: the Stardew Valley Expanded compatibility change (U-SVE-01..04). Build and automated-test execution are unchanged — see [build-instructions.md](build-instructions.md) (`dotnet build Dayswork.sln`) and [unit-test-instructions.md](unit-test-instructions.md) (`dotnet test Dayswork.sln`). The scenarios below are **manual in-game** checks because they exercise SMAPI/live-world seams that the automated suite cannot.

## Environments
- **Vanilla env**: Stardew + SMAPI + Dayswork, **no SVE**. Used to prove vanilla invariance.
- **SVE env**: Stardew + SMAPI + SVE (content `FlashShifter.StardewValleyExpandedCP` and/or `FlashShifter.SVECode`) + a farm-map pack (Grandpa's Farm and/or Frontier / IF2R) + Dayswork.

## Core invariant
- **NFR-SVE-01 Vanilla invariance** — with SVE absent, every Dayswork behavior is byte-for-byte unchanged. The active expansion profile is the Vanilla null-object; all override/work-location lookups return false/None.

---

## S1 — Vanilla invariance (SVE absent)
1. On a vanilla save, hire a worker covering outdoor + a vanilla coop/barn + greenhouse.
2. **Expect**: spawn, entrance, feeding (Deluxe auto-feed skipped), pet/collect, pricing, and product pickup are exactly as before this change. No `[Dayswork]` SVE/profile logs beyond the one-time "active profile = vanilla" line.

## S2 — SVE detection (SVE present)
1. Launch with SVE installed.
2. **Expect**: one-time log that the active expansion profile resolved to **SVE**. No per-shift detection cost.

## S3 — Farm-map worker entrance (U-SVE-02)
1. **Grandpa's Farm**: start a day with a contract. **Expect** the worker spawns at **(112, 51)**; if that tile is blocked, at the nearest passable tile.
2. **Frontier Farm**: **Expect** spawn at **(142, 16)** (or nearest passable).
3. **IF2R**: **Expect** spawn via the warp heuristic (no override) at a sensible entrance.
4. Vanilla Standard Farm (in vanilla env): unchanged warp-heuristic spawn.

## S4 — Premium animal buildings (U-SVE-03)
1. On SVE, own a **Premium Coop** and **Premium Barn** (16 occupants each) with animals.
2. Open the hire screen → **Expect** each premium building is selectable and **prices as its Deluxe counterpart** (Deluxe Coop / Deluxe Barn).
3. Run a shift → **Expect**: the worker does **not** manually feed premium buildings (they auto-feed like Deluxe), and Pet/Collect find nothing already handled by the default auto-petter/auto-grabber (no machine-presence assumption, no errors).

## S5 — Multiple same-type + premium buildings (TODO-08)
1. Own **two base Coops, two base Barns, plus the Premium Coop & Barn**. Select all six.
2. **Expect (selection)**: all six are saved (none dropped as duplicates); the Work Scope summary lists them wrapped inside the panel (e.g., `Barn, Barn, Coop, Coop, SVE_PremiumBarn, SVE_PremiumCoop`), no text overflow.
3. **Expect (runtime)**: the worker **walks** between the buildings (does not teleport/"warp") and services **each** building's animals, including both duplicates.
4. Edit/reload the contract → still resolves and services correctly.

## S6 — Animal-product ground pickup (TODO-07)
1. On SVE, have geese (Goose Egg), camels (Camel Wool), and rabbits (Wool) produce ground products; also pigs (Truffle).
2. **Expect**: the worker collects goose eggs, camel wool, rabbit wool, and truffles, and deposits them per the contract. No infinite "pre-completion rescan picked up 1 new tile item" loop.

## S7 — New content classification + graceful skip (S-24)
1. Have SVE crops, fruit trees, and wild trees (e.g., Birch/Fir) in scope.
2. **Expect**: generic SVE crops/trees are watered/harvested/chopped via the existing data-driven paths; any unclassifiable content is skipped without crashing; no item loss (overflow routes to mail).

## S8 — Standard Grandpa's Farm greenhouse (crop work)
1. On Grandpa's Farm, select the standard greenhouse (`"Greenhouse"`) as scope with crops planted.
2. **Expect**: the worker waters/harvests there as a normal greenhouse. (The separate quest-gated **shed** greenhouse is out of scope — see Known Limitations.)

---

## Known limitations (deferred — verify they degrade gracefully, not crash)
- **TODO-10 — SVE Grandpa's Shed greenhouse (S-25)**: quest-gated, multi-hop location; not serviced (needs multi-hop worker navigation). Selecting/standing in it should simply not be worked; no crash.
- **TODO-09 — Per-building animal work ordering**: the worker currently does all indoor buildings, then all outdoor/grazing animals at once; with spread-out buildings this backtracks. Functional, just not optimally ordered.

## Pass criteria
All S1–S8 behave as described; vanilla env identical to pre-change; no exceptions in the SMAPI log during any scenario; the two known limitations degrade gracefully.
