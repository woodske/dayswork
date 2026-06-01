# Requirements — Stardew Valley Expanded (SVE) Compatibility

**Change**: Make Dayswork compatible with Stardew Valley Expanded without affecting vanilla.
**Status**: Requirements Analysis — generated from answered clarifying questions on 2026-05-29.
**Source inputs**: User request (logged in `audit.md` 2026-05-29T16:10:58Z); answered [sve-compatibility-requirement-verification-questions.md](sve-compatibility-requirement-verification-questions.md); grounded source review of Dayswork and the SVE repo (`C:\Users\kwood\Repos\StardewValleyExpanded`).

---

## 1. Intent Analysis

| Field | Value |
|-------|-------|
| **Request type** | Enhancement / compatibility feature (brownfield) |
| **Scope estimate** | System-wide — worker spawn/entrance, animal-building handling, building navigation, world-content classification, new mod-detection/provider seam, hiring-scope model |
| **Complexity estimate** | Complex |
| **Requirements depth** | Comprehensive |
| **Primary constraints** | (1) zero vanilla behavior change; (2) isolate SVE code; (3) extensible to other expansions; (4) never assume — ground every decision in SVE source / vanilla behavior |

### Guiding principle (NFR-SVE-03, applies to all requirements)
No requirement below licenses an assumption about SVE internals. Each SVE-specific mapping (mod ID, building keys, map/location names, entrance tiles, custom clumps, animal/produce types, Grandpa's Shed interior) **must be confirmed against SVE source** (or vanilla SDV behavior) during Functional Design / Code Generation before it is implemented. Where this document states a source fact, it cites where it was verified; where it defers verification, it says so explicitly.

---

## 2. Functional Requirements

### 2.1 Architecture, detection & isolation

- **FR-SVE-01 — Expansion-compatibility provider seam.** Introduce a provider abstraction representing per-expansion compatibility behavior. A **Vanilla** default provider preserves today's behavior; an **SVE** provider supplies SVE-specific overrides. The provider is the single isolation boundary between vanilla and expansion logic. *(Q1=A; goals 3 & 4)*
- **FR-SVE-02 — Soft runtime detection.** The active provider is selected at runtime by querying the SMAPI mod registry for the SVE mod ID (to be confirmed from SVE's `manifest.json`). The SVE provider activates **only** when SVE is present. There is **no** hard or optional SVE dependency in Dayswork's `manifest.json`. *(Q7=A)*
- **FR-SVE-03 — Vanilla invariance.** When no recognized expansion is detected, the Vanilla provider is used and Dayswork behaves byte-for-byte as it does today. No vanilla code path may change its observable behavior because the seam exists. *(goal 2; NFR-SVE-01)*
- **FR-SVE-04 — Extensible by construction.** Adding compatibility for another expansion (e.g., Ridgeside, East Scarp) must be achievable by implementing a new provider, with **no edits** to vanilla/core call sites. The seam's surface area is defined by the concrete needs below (entrance resolution, animal-building capacity/feeding, building work-location set, content-classification overrides). *(Q1=A; goal 4)*

### 2.2 Farm maps & worker entrance

- **FR-SVE-05 — Supported SVE farm maps.** This change supports the three SVE **replacement farm maps**: **Immersive Farm 2 Remastered**, **Grandpa's Farm**, and **Frontier Farm**. **GrampletonFields is out of scope** for this change. *(Q2=B)*
- **FR-SVE-06 — Entrance/exit resolution.** The dynamic `Farm.warps` "first outdoor exit warp" heuristic (current behavior, with fallback tile `(77,15)`) remains the **default**. The SVE provider supplies an **explicit per-map entrance/exit override only where the heuristic selects the wrong tile**, with each override grounded in that map's actual warp/source data (to be inspected per map during design). The worker must spawn at 6am and exit at a sensible, reachable entrance on each supported SVE map. *(Q3=A; user-flagged concern)*

### 2.3 Animal buildings (Premium Coop & Premium Barn)

- **FR-SVE-07 — Premium building support.** Premium Coop and Premium Barn (verified in SVE `code/Other/Buildings.json` as `IndoorMapType: StardewValley.AnimalHouse`, `MaxOccupants: 16`, feed hopper `(BC)99`) are fully supported for the Feed / Pet / Collect-products tasks, the same as vanilla animal houses.
- **FR-SVE-08 — Data-driven feeding capacity.** The worker's feeding logic must size feeding to the building's **actual** capacity (derived from real trough tiles and/or building `MaxOccupants`), replacing the hardcoded `Deluxe=12 / Big=8 / else=4` ladder in `AnimalTaskHandler.FeedCapacity`. A 16-occupant premium building must have all its troughs fillable (subject to available silo hay). *(Q4=A; verified gap)*
- **FR-SVE-09 — No auto-machine special-casing.** The worker must **not** detect or special-case AutoPetter `(BC)272` or AutoGrabber `(BC)165`. It scans buildings for Pet/Collect work as usual; if installed automation has already petted animals (`wasPet`) or grabbed produce (`currentProduce` empty), the scan naturally finds nothing to do. **Their presence must not be assumed** — the player can move or remove them, and they may exist in vanilla buildings too. *(Q4 refinement)*
- **FR-SVE-10 — Auto-feed detection robustness.** `IsAutoFeedBuilding` must correctly handle premium buildings (it currently matches only `"Deluxe"`). Whether SVE premium buildings auto-feed is to be determined from their map/building source during design; the worker must feed when they do not, and skip naturally (full troughs) when they do. *(Q4=A; verified gap)*
- **FR-SVE-11 — Premium tiers selectable in hiring scope.** SVE premium animal buildings must be representable and selectable wherever the hiring UI enumerates animal buildings. The current `AnimalBuildingTier` enum hardcodes the six vanilla tiers and cannot represent SVE premium buildings; the scope model must accommodate them (exact mechanism decided in design). *(verified gap; required for FR-SVE-07 to be reachable by the player)*

### 2.4 New world content (crops, trees, animals, products)

- **FR-SVE-12 — Rely on data-driven handling.** New SVE crops, trees, and animals/products are handled through Dayswork's existing content-agnostic paths (crops via `HoeDirt`/`Crop`; products via `currentProduce` + `ItemRegistry`; trees via `Tree`/`FruitTree` type). Generic SVE content of these kinds should work without per-item code. *(Q5=A)*
- **FR-SVE-13 — Address only verified gaps.** Explicit SVE handling is added **only** at gaps confirmed against SVE source:
  - **Custom `ResourceClump`s** — `ObjectTargetClassifier` maps clumps by hardcoded vanilla sheet indices; any SVE custom clump must be classified (or deliberately, gracefully skipped) based on confirmed SVE data.
  - **Milk/shear animal-type detection** — `IsMilkProduce`/`IsShearProduce` string-match `Cow`/`Goat`/`Sheep`; any new SVE tool-harvest animal types must be covered.
  - **Special tree species** — confirm SVE trees flow through the `Tree`/`FruitTree` paths; handle any that do not.
  Each gap is verified in SVE source before code is written. *(Q5=A; verified findings)*

### 2.5 Grandpa's Shed

- **FR-SVE-14 — Grandpa's Shed as a work location.** Grandpa's Shed is treated as a **full work location**, valid for whatever applicable indoor tasks its interior actually supports (deposit chests; indoor crops if present). The precise supported task set and navigation (door/warp/entry tile, chest discovery) are **determined from the SVE map source during design**, not assumed here. *(Q6=A)*

### 2.6 Safety & graceful degradation

- **FR-SVE-15 — Graceful skip of unsupported content.** Any SVE content the worker cannot classify or reach is gracefully skipped — never crash — identical to today's vanilla unknown-object handling, logged at debug/trace for maintainers. No new player-facing mail is introduced for this. *(Q8=A)*
- **FR-SVE-16 — Preserve item-safety guarantees.** All existing no-item-loss guarantees (buffer, overflow-to-mail, deposit trips) continue to hold on SVE maps and in SVE buildings.

---

## 3. Non-Functional Requirements

- **NFR-SVE-01 — Isolation / vanilla invariance.** SVE-specific code is isolated behind the provider seam. With SVE absent, there is zero observable change to vanilla behavior. *(goals 2, 3)*
- **NFR-SVE-02 — Extensibility.** A new expansion is added by implementing a new provider; vanilla/core call sites are not modified. *(goal 4)*
- **NFR-SVE-03 — Grounded correctness (no assumptions).** Every SVE mapping is grounded in SVE source or vanilla SDV behavior. Deferred verifications are tracked and resolved before the relevant code is written. *(goal 5)*
- **NFR-SVE-04 — Reliability / safety.** No crashes under SVE; no item loss; graceful degradation per FR-SVE-15/16.
- **NFR-SVE-05 — Testability.** Pure compatibility logic (provider selection, entrance resolution, capacity/feeding derivation, content classification) is unit-tested with xUnit and covered by FsCheck property tests where applicable (PBT full mode — see §4). SVE-asset-dependent behavior is validated via **manual SMAPI playtest with SVE installed**, because automated tests cannot load SVE content. This matches the project's established validation pattern.
- **NFR-SVE-06 — Performance.** Compatibility resolution must not regress the runtime performance envelope established by the Worker Routing change. Provider selection and SVE lookups are resolved once / cached; no per-tile reflection or per-frame mod-registry queries in hot paths.
- **NFR-SVE-07 — Maintainability.** SVE-specific identifiers (mod ID, building data keys, map/location names, entrance overrides) are centralized in the SVE provider, not scattered as magic strings across the codebase.

---

## 4. Extension Configuration (for this change)

| Extension | Decision | Source |
|---|---|---|
| **Security Baseline** | **Disabled** — no network/PII/auth/secrets surface | Q9=A |
| **Property-Based Testing** | **Enabled — full mode** (FsCheck + xUnit), blocking where applicable | Q10=A |

---

## 5. Out of Scope (this change)

- **GrampletonFields** support *(Q2=B)*.
- Concrete providers for expansion mods **other than SVE** (the seam must accommodate them, but only the Vanilla and SVE providers are implemented now).
- Any worker-behavior change not required for compatibility.
- Changes to the parked Worker Routing change.
- Custom worker art / richer tool visuals (pre-existing TODO-06, unrelated).

---

## 6. Key Requirements Summary

1. A runtime-selected **provider seam** isolates SVE behavior; Vanilla path is unchanged and the design is extensible to future expansions (**FR-SVE-01..04**).
2. Worker spawns/exits correctly on **Immersive Farm 2 Remastered, Grandpa's Farm, and Frontier Farm**, keeping the warp heuristic with **per-map overrides only where needed** (**FR-SVE-05..06**).
3. **Premium Coop/Barn** work fully: **data-driven feeding capacity** (fixing the 16-animal underfeed), **no auto-machine special-casing** (scan-and-skip), and **premium tiers selectable** in hiring scope (**FR-SVE-07..11**).
4. New SVE **crops/trees/animals/products** rely on existing data-driven paths; explicit code only at **verified gaps** (custom clumps, milk/shear types, special trees) (**FR-SVE-12..13**).
5. **Grandpa's Shed** is a full work location, with its supported tasks/navigation determined from SVE source (**FR-SVE-14**).
6. Unsupported content is **gracefully skipped** with item-safety preserved (**FR-SVE-15..16**).
7. Everything is **grounded in source, isolated, extensible, vanilla-safe**, and validated by unit/property tests plus manual SVE playtest.
