# Manage Crops — Component Methods

High-level method signatures (indicative; final names + detailed business rules in
per-unit Functional Design). All Core methods are pure/deterministic.

---

## C-25 PlantingViabilityCalculator
- `bool IsViable(CropDescriptor crop, GameDate today, int seasonLengthDays, bool seasonAgnosticLocation)` — fertilized growth time vs days remaining; always `true` when `seasonAgnosticLocation`.
- `int DaysToMaturity(CropDescriptor crop, bool fertilized)` — helper.

## C-26 CropSupplyPlanner
- `SupplyTarget ComputePurchaseTarget(CropPlan plan, GreenhouseContext ctx, IReadOnlyList<PlannedTile> emptyViableTiles)` — seeds/fertilizer counts to buy this shift.
- `int CompletableTiles(int seedsOnHand, int fertilizerOnHand, bool zoneUsesFertilizer)` — `zoneUsesFertilizer ? min(seeds,fert) : seeds`.
- `bool BothComponentsOnHand(SeasonCropChoice choice, SupplyInventory inv)` — never-lay-one-without-both gate.

## C-27 SeasonAssignmentResolver
- `CropZoneAssignment ApplyChoice(CropZoneAssignment zone, Season season, CropDescriptor crop, string? fertilizerId, bool autoReplant)` — sets the season, auto-populates + locks multi-season spans.
- `bool IsSeasonLocked(CropZoneAssignment zone, Season season)`.
- `IReadOnlyList<Season> MultiSeasonSpan(CropDescriptor crop, Season chosen)`.

## C-28 StoreResolver
- `StoreResolution Resolve(StorePreference pref, DayOfWeek day, int timeOfDay, bool isFestival)` → `{ Store (Pierre|Joja|None), UsingFallback, ClosedReason }`.
- `bool StoreStocks(Store store, string itemId, ShopStockSnapshot stock)`.

## C-29 CropShiftPlanner
- `ManagedCropShiftPlan BuildPlan(CropPlan plan, FieldState field, SupplyInventory input, GreenhouseContext ctx, GameDate today)` — partitions supply-independent vs supply-dependent work, per-tile action lists in dependency order, replant/gap-fill selection.
- `IReadOnlyList<TileAction> OrderTileActions(TileState tile, SeasonCropChoice choice)` — harvest → clear debris → till → fertilize → seed → water.

## C-30 CropPlanSerialization
- `CropPlanDtoV3 ToDto(CropPlan plan)` / `CropPlan FromDto(CropPlanDtoV3 dto)` — round-trip (PBT-02).
- `ContractDtoV3 Upgrade(ContractDtoV2 v2)` — empty/disabled crop plan on migration.

## C-31 CropDescriptor
- (data) `{ SeedItemId, CropItemId, IReadOnlyList<Season> Seasons, int FertilizedGrowthDays, int BaseGrowthDays, bool IsAutoBuyable, bool IsChestSupplyOnly, bool IsRegrow }`.

---

## M-24 ManageCropsMenu
- `void Open(ContractDraft draft)` / `void Draw(SpriteBatch b)` / input handlers (mouse/keyboard/gamepad).
- `void SelectSeason/SelectCrop/SelectFertilizer/ToggleAutoReplant(...)` — authoring actions writing the draft crop plan.
- `void BeginDraw()` — hand off to the zone-draw overlay to apply the configured plan.
- `string StatusChip()` → "Done" | "Optional".

## M-25 CropCatalogProvider
- `IReadOnlyList<CropDescriptor> GetCrops(Season? seasonFilter, bool greenhouseContext)` — vanilla+modded, season-filtered unless greenhouse/shed.
- `bool IsStockedAt(Store store, string seedId)` — for auto-buyable tagging.

## M-26 ShopPurchaseService
- `PurchaseResult BuyHeadless(Store store, IReadOnlyList<PurchaseLine> lines, Farmer wallet, IInventory carry)` — reads live `Data/Shops`/`ShopBuilder`, deducts gold, grants items; returns per-line outcomes for paced notices.
- `ShopStockSnapshot ReadStock(Store store)`.

## M-27 ManagedCropShiftRunner
- `void RunManagedCropBatch(ManagedCropShiftPlan plan, ShiftContext shift)` — executes per-tile beats, drives the store trip (navigator + M-26), routes harvest output.
- `bool IsTileDiggable(GameLocation loc, TileCoord tile)` — live-map `Diggable` check (correct map variant).
- `void RouteHarvest(Item item, CropZoneAssignment zone)` — to assigned chest or output-chest fallback.

## M-28 CabinChestService
- `void DeclareChests(BuildingData data)` — add input chest entry to `BuildingData.Chests`.
- `void EnsureInputChest(Building office)` — one-time idempotent backfill.
- `void ApplyProgrammaticNames(Building office)` — i18n input/output labels.
- `bool IsBuiltInOfficeChest(GameLocation loc, TileCoord tile)` — used by `ChestResolver` exclusion.

## M-29 CropHudNotifier
- `void PurchaseCompleted(string itemName, int qty)` / `InsufficientFunds()` / `FestivalSkipped()` / `FertilizerUnavailable(zone)` / `UsingFallbackStore(store)` / `ToolMissing(action, tile)`.

---

## Existing components — added methods (indicative)
- `CrossLocationRouteNavigator` — town-store route definitions (Farm↔SeedShop, Farm↔JojaMart) consumed by the existing `Navigate(...)` path.
- `WorkScopeSet` — `ManagedCropWorkScope? ManagedCrops { get; }`.
- `ContractDraft` — `CropPlan CropPlan { get; }` (in-progress).
- `WorkerToolExtensions.ForTask` — map the till action → `WorkerTool.Hoe`.
- `WorkerEnergyProfile.ActionCosts` — `HoeSwing`, `PlantSeed`, `ApplyFertilizer` entries.
