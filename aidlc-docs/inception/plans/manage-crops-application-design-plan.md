# Application Design Plan — Manage Crops

**Stage**: Application Design (Inception)
**Source**: [manage-crops-requirements.md](../requirements/manage-crops-requirements.md) (FR-MC-01..44, NFR-MC-01..09); stories S-27..S-35.
**Existing design**: components C-01..C-23 (Core) and M-01..M-23 (Mod) in [components.md](../application-design/components.md) + [sve-compatibility-application-design.md](../application-design/sve-compatibility-application-design.md). New Manage Crops components will be **C-24+ / M-24+**.

## Design scope
Define the new components, methods, services, and dependencies for Manage Crops — **high-level** only (detailed business logic comes in per-unit Functional Design). Key new surfaces: crop-plan domain + pure planning logic, managed-crop work-scope integration, authoring UI, draw-overlay extension, crop catalog, town-store navigation, headless shop transaction, second cabin chest, persistence V3/migration, energy/capability additions, greenhouse/shed support.

---

## Design Questions

Please answer each by filling in the letter after the `[Answer]:` tag. Each notes the recommended option and how it fits the existing architecture.

### Question 1 — Crop-plan domain & pure planning logic placement
Where should the crop-plan domain and the pure decision logic (viability, supply targeting / `min(seeds,fertilizer)`, multi-season locking, store/fallback resolution, per-tile action ordering) live?

A) **New dedicated `Dayswork.Core` components** — crop-plan domain types plus pure planner components (e.g. `CropPlanningService`/`PlantingViabilityCalculator`/`SupplyPlanner`/`SeasonAssignmentResolver`/`StoreResolver`), peers to the existing pure-Core components (C-01..C-18). Keeps everything SMAPI-free and FsCheck-testable (NFR-MC-01/09, S-35). (Recommended)
B) Fold the planning logic into existing components (e.g. extend `ShiftPlanBuilder`/`DepositPlanner`) rather than adding new Core components.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Town-store navigation
How should walking to Pierre's / JojaMart be modeled? (The spec calls this "new but analogous" work on the existing routing layer.)

A) **Extend the existing cross-location routing layer** (`CrossLocationRouteNavigator` / `BuildingWorkNavigator`) with **vanilla town-store route definitions** (Farm ↔ SeedShop / JojaMart), keeping it in the core navigation path (not the SVE expansion seam, since these are vanilla locations). (Recommended)
B) Add a separate dedicated town-shopping navigator component independent of the existing route navigator.
C) Put town-store routes in the expansion-profile seam (`IExpansionProfile`) alongside SVE routes.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3 — Headless shop transaction
How should the headless 1.6 purchase (read `Data/Shops` stock/prices via `ShopBuilder`, deduct gold, grant items) be structured?

A) **A thin Mod-side adapter** (e.g. `ShopPurchaseService`, M-2x) that touches the live game/shop APIs, driven by a **pure-Core purchase planner** (what/how-many to buy, affordability, store-stocks-it gating) — mirrors the Mod-adapter + pure-Core split used elsewhere. (Recommended)
B) Resolve purchases inline in `ShiftOrchestrator` without a dedicated service.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Managed-crop work-scope & shift integration
How should managed-crop work enter the runtime?

A) **New `ManagedCropWorkScope` as a peer in `WorkScopeSet`** (alongside `OutdoorWorkScope`/`GreenhouseWorkScope`) plus new managed-crop batch handling in `ShiftPlanBuilder`/`ShiftOrchestrator` — consistent with the existing scope→batch runtime model. (Recommended)
B) Reuse the existing greenhouse/outdoor scopes and tag crop work onto them rather than adding a new scope.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 5 — Authoring UI structure
How should the Manage Crops page be built?

A) **A new dedicated hub page** (`ManageCropsMenu`, M-2x) reached from `HubMenu`, following the existing hub-page pattern (peer of `EnergyMenu`, `OutputDestinationsMenu`, `TaskSelectionMenu`), driven by an extended `ContractDraft`. (Recommended)
B) Extend an existing menu (e.g. fold crop authoring into `ZoneAndChestMenu`) rather than a new page.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 6 — Crop catalog (data source for the crop list)
How should the available-crop list (vanilla + modded, season filter, auto-buyable vs chest-supply-only tagging) be sourced?

A) **A dedicated catalog seam** — a Mod-side reader (e.g. `CropCatalogProvider`, M-2x) that reads live game crop data (`Data/Crops`) and shop stock to produce **pure crop descriptors**, consumed by the UI and the Core planners. Keeps game-data access in the Mod and descriptors pure/testable. (Recommended)
B) Read crop/shop data ad hoc directly in the menu code without a dedicated catalog component.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Mandatory Design Artifacts (to be produced after answers approved)
- [ ] `application-design/manage-crops-components.md` — new components C-24+/M-24+ (name, purpose, responsibilities, interfaces).
- [ ] `application-design/manage-crops-component-methods.md` — high-level method signatures (business rules deferred to Functional Design).
- [ ] `application-design/manage-crops-services.md` — service definitions + orchestration (shift integration, shopping, persistence).
- [ ] `application-design/manage-crops-component-dependency.md` — dependency matrix + data-flow + vanilla/SVE seam boundaries.
- [ ] `application-design/manage-crops-application-design.md` — consolidated design doc.
- [ ] Validate design completeness/consistency against FR-MC-*/NFR-MC-* and the existing architecture.

---

When you've answered the six questions, let me know (e.g. "done"). I'll check for ambiguity/contradiction and, once answers are settled, generate the design artifacts.
