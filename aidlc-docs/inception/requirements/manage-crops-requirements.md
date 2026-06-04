# Requirements — Manage Crops

**Status:** Approved gate pending
**Phase:** Inception / Requirements Analysis
**Date:** 2026-06-04
**Source spec:** [`manage-crops-spec.md`](../manage-crops-spec.md) (all OQ-1..OQ-8 resolved; decisions log §10–§11)

---

## Intent Analysis

- **User request:** "Using ai-dlc, implement new manage crops feature following `aidlc-docs/inception/manage-crops-spec.md`."
- **Request type:** New Feature (large, multi-component).
- **Scope estimate:** System-wide — new hub page + authoring UI, new domain/work-scope layer, runtime shift behavior, autonomous town shopping with new cross-location navigation, two cabin chests, persistence schema bump + migration, greenhouse/SVE-shed support.
- **Complexity estimate:** Complex.
- **Requirements depth:** Comprehensive.
- **Delivery model (Q1=A):** Full spec is in scope, delivered through the normal AI-DLC per-unit loop (decompose into sequential units in Units Generation).

---

## Clarification Answers (this change)

| # | Decision | Answer |
|---|----------|--------|
| Q1 | Deliver full spec via the unit loop (entire spec in scope) | A |
| Q2 | "Preferred store closed → using fallback" HUD notice is in scope | A |
| Q3 | Input-chest backfill for pre-existing saved offices is **required** | A |
| Q4 | Both global toggles ("clear debris", "clear dead plants") default **ON** | A |
| Q5 | Security Baseline extension **disabled** | B |
| Q6 | Property-Based Testing extension enabled, **full mode** | A |

No contradictions detected across answers; all align with standing project decisions.

---

## Functional Requirements

### Authoring & UI
- **FR-MC-01** Add a top-level **Manage Crops** button to `HubMenu`, rendered like existing nav rows, opening a new dedicated page (e.g. `ManageCropsMenu`). Status chip shows "Done" when ≥1 zone is configured, "Optional" otherwise. (§5.1)
- **FR-MC-02** Authoring is **crop-first then draw**: the player configures the full seasonal plan (season → crop → fertilizer → optional auto-replant), optionally assigns an output chest, then draws one or more zones in a single draw that applies the whole seasonal plan. (§5.2)
- **FR-MC-03** The crop list is built from game crop data (vanilla **and** modded). On the open farm it is filtered to the selected season; in the greenhouse / Grandpa's Shed it is **not** season-filtered. Each crop is tagged **auto-buyable** (stocked at Pierre/Joja) vs **chest-supply-only**. (§5.2, OQ-3)
- **FR-MC-04** Multi-season crops (e.g. corn) auto-populate and **lock** their consecutive seasons; locked seasons are styled distinctly, are non-assignable, and the UI makes clear why. (§5.4)
- **FR-MC-05** Greenhouse / Grandpa's Shed: present **no season option**; the zone has a single continuous crop assignment. (§5.4)
- **FR-MC-06** Reuse the existing zone-draw overlay machinery (`ZoneDrawOverlay`, `ZoneDrawMenu`, `IZoneDrawSource`), extended so already-assigned tiles are **unselectable** and rendered in **red** (a single color for all existing assignments), while the current draw session renders in **green** until confirmed. Overlap is prevented. (§5.3) **DEV-MC-01 (user override 2026-06-04):** the spec §5.3 called for a distinct color *per crop* with the current draw in red; per user direction, existing zones are a single **red** and the active draw is **green**.
- **FR-MC-07** Editing is **delete-and-redraw only** — no in-place reassignment of a zone's crop/fertilizer/shape/chest this iteration. (§5.5, Non-Goals)
- **FR-MC-08** Two non-contiguous zones may carry the same plan; each drawn zone is its own independently-configured unit. (§5.2)

### Shift Behavior
- **FR-MC-09** Per-shift work is scheduled around store hours and ordered by **dependency**, not wall-clock: assess supplies vs input-chest stock → do supply-independent work (harvest, clear debris, till, clear dead, water existing, fertilize+seed from chest stock) during the 6–9 AM window → if more supplies needed and not a festival, shop once stores open (~9 AM) → return, fertilize+seed purchased tiles, water new plantings → continue field work to end of shift. (§6.1)
- **FR-MC-10** Per-tile dependency order: **harvest (if mature) → clear debris → till → fertilize → seed → water**, each action its own animation/beat paced at `WorkerActionAnimationMs`; fertilizer is laid on bare tilled soil before the seed. Harvest-first enables same-shift replanting. (§6.1, OQ-8)
- **FR-MC-11** **Fertilizer & seed atomicity:** the farmhand never lays fertilizer **or** seed on a tile unless **both** required components are on hand for that tile. Partial stock completes only `min(seeds, fertilizer)` tiles; leftovers stay in the input chest. (§6.1, §6.3, OQ-8)

### Purchasing
- **FR-MC-12** Funding is the **player's wallet**; buy as much as affordable (max affordable quantity on insufficient funds, then continue). Quantity target = empty assigned **viable** tiles planned this shift, computed up front during plan assessment. (§6.2)
- **FR-MC-13** Availability gate: only **use** seeds/fertilizer physically present in the input chest; if absent, attempt to buy **only** from a store that stocks the item. (§6.2)
- **FR-MC-14** **Chest-supply-only crops** (ancient fruit, coffee, foraged, seed-maker output) are planted only from input-chest stock; never trigger a store trip; on empty stock, plant what's possible and leave the rest (no purchase notification). (§6.2)
- **FR-MC-15** Store selection: contract-configured **preferred store** (Pierre / Joja / Either) with **fallback** when preferred is closed (e.g. Pierre on Wednesdays). Pierre = unlimited stock; JojaMart shoppable **without membership**. Stores open 9 AM (Pierre 9–5 closed Wed; Joja 9–11 daily). (§6.2)
- **FR-MC-16** Festival days: purchasing is **skipped** with a HUD notice; the rest of the shift still runs. (§6.2, §7)
- **FR-MC-17** **Physical travel:** the farmhand walks to and enters the store. Routing to a **town store** (SeedShop / JojaMart) is **new** cross-location navigation built on the existing layer (`CrossLocationRouteNavigator`, `BuildingWorkNavigator`). (§6.2)
- **FR-MC-18** **Headless transaction:** at the counter, resolve the purchase without opening the visual `ShopMenu`, reading live 1.6 shop stock/prices (`Data/Shops` via `ShopBuilder`) so mod-added seeds, price mods, seasonal gating, and Joja markup are respected. Deduct gold from the wallet; place purchases into the worker's carried inventory; return leftovers to the **input chest** at end of shift. (§6.2, OQ-4)
- **FR-MC-19** **Paced transactions:** execute purchases one line-item per beat at `WorkerActionAnimationMs`, each beat emitting its own HUD notification (e.g. "Bought 12× Parsnip Seeds"). (§6.2, §7)
- **FR-MC-20** Emit a HUD notice when the **preferred store is closed** and the farmhand falls back to the other store. (§7 candidate — confirmed in scope, Q2=A)

### Planting Viability
- **FR-MC-21** On the open farm, plant only if the crop can **mature and be harvested at least once before the season ends**, computed with **fertilized** growth time. (§6.3)
- **FR-MC-22** If a zone's configured fertilizer is **entirely unavailable** (not in chest, not purchasable at preferred/fallback store), **no** tiles in that zone are planted and a HUD notice fires — never plant seed un-fertilized. Partial fertilizer stock completes only fully-supplied tiles. (§6.3, §7)
- **FR-MC-23** **Greenhouse / shed bypass:** in season-agnostic locations (`Greenhouse`, `Custom_GrandpasShedGreenhouse`, both `IsGreenhouse`) the end-of-season viability gate is **not** applied. (§6.3, §9)

### Field Maintenance
- **FR-MC-24** **Per-season auto-replant** toggle: when enabled, harvest-first lets a non-regrow crop's freed tile be replanted the **same shift** (viability + supply permitting; soil stays tilled so only fertilize→seed→water needed), and each shift fills any empty prepared tiles that still have enough days to produce. When disabled, emptied tiles are not refilled within the season. (§6.4)
- **FR-MC-25** **Re-till** managed-zone tiles that reverted to untilled each shift. Tilling/planting targets only tiles the game marks tillable (`Diggable` Back-layer property) and not blocked by an unclearable object; non-diggable tiles in a drawn zone are skipped. (§6.5)
- **FR-MC-26** **Global toggle "clear debris before tilling"** (default **ON**, Q4=A): when enabled, clear debris blocking a tile before tilling, independently of general clearing tasks; no energy spent when a tile has no debris. (§6.5)
- **FR-MC-27** **Global toggle "clear dead plants"** (default **ON**, Q4=A): when enabled, clear dead/wilted plants **opportunistically as encountered**, scoped to the contract's **assigned work areas only — not farm-wide**; no dedicated sweep. When disabled, dead-crop tiles cannot be re-tilled/replanted and are skipped. (§6.6, OQ-5)

### Coexistence & Output
- **FR-MC-28** `WaterCrops` and `HarvestCrops` remain available as general tasks for manually-planted crops outside managed zones; inside managed zones watering/harvesting are automatic. The two paths must not double-act on the same tile in the same shift. (§6.7)
- **FR-MC-29** Per-zone output routing: each zone routes harvest to its **assigned chest** (`ChestRef`); a zone with no chest assigned falls back to current behavior (hold in inventory, deposit to the office **output chest** at end of shift). (§6.8)

### Tools & Capability Gating
- **FR-MC-30** The farmhand uses whichever tools it has equipped for the contract (no separate tool config). Crop actions reuse the existing capability model: add `WorkerTool.Hoe` (till maps to it); watering→`WateringCan`; debris/dead-plant clearing→`Axe`/`Pickaxe`/`Scythe` respecting `CapabilityMatrix` level gating. (§6.9, OQ-7)
- **FR-MC-31** Planting and fertilizing are **not** tool-gated — they gate on item availability. (§6.9)
- **FR-MC-32** Missing/under-leveled tool → **skip just that action/tile + HUD notice** at runtime; the rest of the contract proceeds. **No** contract-creation tool validation. (§6.9)

### Cabin Chests
- **FR-MC-33** Add a second built-in office chest — the **input chest** (`Bindicle.Dayswork_Input`, new) — alongside the existing **output chest** (`Bindicle.Dayswork_Output`), both via the 1.6 `BuildingData.Chests` list on `HiringBuilding.BuildData()`, both ordinary player-accessible chests with a `DisplayTile` (e.g. symmetric porch tile). (§6.10)
- **FR-MC-34** The input chest is the player-stocked supply reservoir: the availability gate checks it first, and leftover purchased supplies return here at end of shift. (§6.10)
- **FR-MC-35** `ChestResolver` excludes **both** built-in chests from the player-selectable per-zone destination list. (§6.10)
- **FR-MC-36** Name input/output chests **programmatically** with fixed i18n-backed labels (surfaced via vanilla hover tooltip + Lookup Anything); all other chests keep existing `ChestResolver.GetDisplayName` behavior. (§6.10)

### Persistence & Migration
- **FR-MC-37** Add new per-contract crop-plan domain types in `Dayswork.Core` (`CropPlan`, `CropZoneAssignment`, `SeasonCropChoice`, `StorePreference`, `ManagedCropWorkScope`) and DTOs under `Dayswork.Core/Persistence/Dto/`. (§8.1, §8.3)
- **FR-MC-38** Bump save schema `DaysworkSaveDataV2`→`V3` and `ContractDtoV2`→`V3`. Existing contracts deserialize with an empty/disabled crop plan (feature opt-in; absence = no managed crops). (§8.3)
- **FR-MC-39** **Input-chest backfill (required, Q3=A):** on load, ensure the input chest exists for pre-existing `Bindicle.Dayswork_Office` buildings; if the game does not auto-create a newly-declared `BuildingChest` on already-built instances, add a one-time backfill. (§8.3)

### Energy
- **FR-MC-40** Add new `WorkActionKind`s with **configurable non-zero** energy costs: `HoeSwing` (till), `PlantSeed`, `ApplyFertilizer`. Debris/dead-plant clearing reuse existing `AxeSwing`/`PickaxeSwing`/`ScytheSwing`; watering/harvesting reuse `WaterTile`/`HarvestCrop`/`HarvestFruit`. New costs added to `WorkerEnergyProfile.ActionCosts` and surfaced via config/GMCM. (§8.2, OQ-2)
- **FR-MC-41** Shopping trips cost shift **time only, not energy**. (§8.2, OQ-2)
- **FR-MC-42** Pricing is unaffected — no separate charge for crop management; crop work draws the existing flat energy-tier budget. The only added gold cost is the seed/fertilizer purchase. (§8.2, OQ-1)

### Compatibility
- **FR-MC-43** Greenhouse and Grandpa's Shed (SVE `Custom_GrandpasShedGreenhouse`) are in scope and **reuse** the existing `GreenhouseWork` role and pre-built routes — no new navigation work for the shed. `Custom_GrandpasShed` proper is `DepositOnly`. (§9)
- **FR-MC-44** Resolve the plantable area **per tile at runtime** via the live map's `Diggable` Back-layer property — no hardcoded region. At impl time, confirm the farmhand reads `Diggable` from whichever map variant (default vs `...Cleared`) is live at shift time. (§6.5, §9 watch item)

## Non-Goals (explicitly out of scope)
- Indiscriminate/open-field tilling (tilling only inside managed zones). (§3)
- In-place zone editing. (§3)
- Quality-based output splitting. (§3)
- Non-crop plantings: fruit trees, tea bushes/saplings, garden pots (`HoeDirt` seed-crops only). (§3)
- Multiplayer (gated by `MultiplayerGuard`; single-player only). (§3)

---

## Non-Functional Requirements

- **NFR-MC-01 Determinism:** all pure planning/decision logic (viability math, supply targeting, `min(seeds,fertilizer)` completion, season assignment & multi-season locking, store/fallback resolution, per-tile action ordering) must be deterministic and pure-Core for testability. (Property-Based Testing full mode applies.)
- **NFR-MC-02 Performance:** per-shift planning and per-tile checks must keep the shift loop responsive (no per-tile hot-path graph discovery); town-store routing reuses bounded navigation, consistent with the existing synchronous runtime shell.
- **NFR-MC-03 Item & gold safety:** never lose purchased items or harvested output; leftovers settle to the input chest, harvest to the assigned/output chest; never deduct gold without delivering the corresponding goods to carried inventory.
- **NFR-MC-04 Vanilla / no-SVE invariance:** when SVE is absent, behavior is unchanged from vanilla; greenhouse/shed handling degrades cleanly via the existing expansion profile seam.
- **NFR-MC-05 Resilience:** missing/under-leveled tools, closed stores, festivals, insufficient funds, unavailable fertilizer, and partial stock are all handled gracefully (skip + notify), never throwing or aborting the shift.
- **NFR-MC-06 Backward-compatible persistence:** V2 saves load into V3 with an empty/disabled crop plan; the input-chest backfill is one-time and idempotent.
- **NFR-MC-07 i18n:** all new player-facing text (menu labels, chest names, HUD notifications) is i18n-backed and passes the existing hardcoded-string lint gate.
- **NFR-MC-08 Test rigor:** examples + FsCheck properties (full mode) for the pure logic above and save round-trips; manual SMAPI playtest scenarios for authoring, planting/harvest, shopping trip, two-chest behavior, and greenhouse/SVE-shed.
- **NFR-MC-09 Tech stack:** reuse the existing C#/.NET 6 + SMAPI + xUnit + FsCheck stack and existing navigation/capability/energy/persistence seams; no new runtime dependencies.

---

## Extension Configuration (this change)
| Extension | Enabled | Mode | Decided At |
|---|---|---|---|
| Security Baseline | No | — | Requirements Analysis (Q5=B — no network/PII/auth surface) |
| Property-Based Testing | Yes | Full — all PBT rules blocking where applicable | Requirements Analysis (Q6=A) |

---

## Key Requirements Summary
- New **Manage Crops** hub page with crop-first-then-draw authoring over a new managed-crop work-scope layer.
- Deterministic, viability-gated, self-healing per-shift crop management with harvest-first per-tile ordering and seed/fertilizer atomicity.
- Autonomous, store-hours-aware **town shopping** with **new** cross-location store navigation and **headless** live-1.6 shop transactions, paced with per-transaction HUD notices.
- **Two** built-in office chests (input + output) with programmatic i18n names and required input-chest backfill for existing saves.
- Save schema **V2→V3** with empty-plan migration; new energy action kinds (hoe/plant/fertilize) that cost configurable energy; shopping costs time only.
- Greenhouse + SVE Grandpa's-Shed greenhouse support reusing existing routes; per-tile `Diggable` plantable-area resolution.
