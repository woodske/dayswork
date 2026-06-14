using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

internal sealed record FeedWorkPlan(
    IReadOnlyList<WorkItem> WorkItems,
    TileCoord HopperTile,
    int HayToTake,
    int EmptySlots);

internal sealed class AnimalTaskHandler
{
    private readonly IMonitor _monitor;

    public AnimalTaskHandler(IMonitor monitor) => _monitor = monitor;

    public IReadOnlyList<(AnimalRef Ref, FarmAnimal Live)> EnumerateAnimals(
        GameLocation location,
        IReadOnlySet<string>? selectedHomeLocations = null)
    {
        var result = new List<(AnimalRef Ref, FarmAnimal Live)>();

        if (location is AnimalHouse animalHouse)
        {
            foreach (var pair in animalHouse.Animals.Pairs)
                AddIfSelected(pair.Value, selectedHomeLocations, result);
        }
        else if (location is Farm farm)
        {
            foreach (var pair in farm.Animals.Pairs)
                AddIfSelected(pair.Value, selectedHomeLocations, result);
        }

        return result;
    }

    public FarmAnimal? FindLiveAnimal(GameLocation location, AnimalRef animalRef)
    {
        if (location is AnimalHouse animalHouse &&
            animalHouse.Animals.TryGetValue(animalRef.Id, out var housed))
            return housed;

        if (location is Farm farm &&
            farm.Animals.TryGetValue(animalRef.Id, out var grazing))
            return grazing;

        return null;
    }

    public FeedWorkPlan CreateFeedWork(GameLocation animalHouseLocation)
    {
        if (animalHouseLocation is not AnimalHouse animalHouse)
            return EmptyFeedWork();

        var parent = animalHouse.ParentBuilding;
        if (IsAutoFeedBuilding(parent?.buildingType.Value, animalHouseLocation))
            return EmptyFeedWork();

        // Data-driven capacity (trough count clamped to the building's real max occupants) via the
        // expansion compat seam; falls back to the legacy tier ladder when the seam is unavailable
        // (e.g., unit tests / compat not yet resolved). Fixes premium-building under/over-feeding
        // without changing vanilla capacities (parity covered by tests).
        var capacity = ModEntry.ExpansionCompat?.ResolveAnimalFeedCapacity(animalHouse)
                       ?? FeedCapacity(parent?.buildingType.Value);
        var filled = Math.Clamp(CountPlacedHay(animalHouse), 0, capacity);
        var emptySlots = Math.Max(0, capacity - filled);
        if (emptySlots == 0)
            return EmptyFeedWork();

        var farmHay = Game1.getFarm().piecesOfHay.Value;
        if (farmHay <= 0)
        {
            _monitor.Log(I18nHelper.Get("log.animal.no_silo", new { location = animalHouse.Name }), DevLog.WarnLevel);
            return EmptyFeedWork(emptySlots);
        }

        var emptyTroughTiles = ResolveEmptyTroughTiles(animalHouseLocation)
            .Take(Math.Min(emptySlots, farmHay))
            .ToList();
        if (emptyTroughTiles.Count == 0)
        {
            _monitor.Log(
                $"[Dayswork][feed-plan] location={animalHouse.Name} unable to discover empty trough tiles; skipping visible feed work. troughs=0 filled={filled} empty={emptySlots}",
                DevLog.WarnLevel);
            return EmptyFeedWork(emptySlots);
        }

        var hasHopper = TryResolveHopperTile(animalHouseLocation, null, out var hopperTile, out var hopperSource);

        if (!hasHopper)
        {
            _monitor.Log(
                $"[Dayswork][feed-plan] location={animalHouse.Name} unable to discover feed hopper; skipping visible feed work. troughs={emptyTroughTiles.Count} hopper=<null>",
                DevLog.WarnLevel);
            return EmptyFeedWork(emptySlots);
        }

        var hopperNavTiles = StandTilesAround(hopperTile, animalHouseLocation);
        var hopperNav = hopperNavTiles.FirstOrDefaultPassable(animalHouseLocation) ??
                        hopperNavTiles.FirstOrDefaultNullable() ??
                        hopperTile;
        var feedItems = new List<WorkItem>
        {
            new(hopperNav, hopperTile, TaskKind.FeedAnimals, animalHouseLocation.Name, null,
                hopperNavTiles.Count > 0 ? hopperNavTiles : new[] { hopperTile }),
        };

        var feederItems = emptyTroughTiles
            .Select(tile =>
            {
                var standTiles = FeedStandTiles(tile, hopperTile, animalHouseLocation);
                var navTile = standTiles.FirstOrDefaultPassable(animalHouseLocation, hopperTile) ??
                              standTiles.FirstOrDefaultNullable() ??
                              hopperNav;
                return new WorkItem(
                    navTile,
                    tile,
                    TaskKind.FeedAnimals,
                    animalHouseLocation.Name,
                    null,
                    standTiles.Count > 0 ? standTiles : new[] { navTile });
            })
            .ToList();

        feedItems.AddRange(feederItems);
        DevLog.Log(
            $"[Dayswork][feed-plan] location={animalHouse.Name} hopper=({hopperTile.X},{hopperTile.Y}) hopperNav=({hopperNav.X},{hopperNav.Y}) hopperSource={hopperSource} troughSource=Back:Trough filled={filled} empty={emptySlots} hayToTake={feederItems.Count} feeders=[{string.Join("; ", feederItems.Select(item => $"task=({item.TaskTile.X},{item.TaskTile.Y}) nav=({item.NavTile.X},{item.NavTile.Y})"))}]");
        return new FeedWorkPlan(feedItems, hopperTile, feederItems.Count, emptySlots);
    }

    public bool TakeHay(GameLocation animalHouseLocation, int count)
    {
        if (animalHouseLocation is not AnimalHouse || count <= 0)
            return false;

        var farm = Game1.getFarm();
        var taken = Math.Min(count, farm.piecesOfHay.Value);
        if (taken <= 0)
            return false;

        farm.piecesOfHay.Value -= taken;
        animalHouseLocation.playSound("shwip");
        return true;
    }

    public bool PlaceHay(GameLocation animalHouseLocation, TileCoord troughTile)
    {
        if (animalHouseLocation is not AnimalHouse animalHouse)
            return false;

        var tileVec = new Vector2(troughTile.X, troughTile.Y);
        if (animalHouse.objects.ContainsKey(tileVec))
            return false;

        if (animalHouseLocation.doesTileHaveProperty(troughTile.X, troughTile.Y, "Trough", "Back", false) is null)
            return false;

        var hay = ItemRegistry.Create<SObject>("(O)178", 1);
        if (!animalHouse.dropObject(hay, tileVec * 64f, Game1.viewport, initialPlacement: false, who: Game1.player))
            return false;

        animalHouseLocation.playSound("grassyStep");
        return true;
    }

    public bool ShouldPet(FarmAnimal animal) => !animal.wasPet.Value;

    public bool Pet(FarmAnimal animal)
    {
        if (!ShouldPet(animal))
            return false;

        animal.pet(Game1.player, is_auto_pet: false);
        return true;
    }

    public bool HasToolHarvestReady(FarmAnimal animal) =>
        !string.IsNullOrWhiteSpace(animal.currentProduce.Value) &&
        !ProducesGroundForage(animal);

    /// <summary>
    /// True for animals (e.g. pigs) whose produce is dug up from the ground rather than harvested
    /// off the animal. Their produce — truffles — is dropped on the ground and collected by the
    /// farm-forage pass, never taken directly from the animal. Excluding them stops the worker from
    /// "harvesting" a truffle off each pig (notably indoors in winter, where pigs never forage).
    /// Falls back to allowing collection if the animal data is unavailable (unchanged behaviour).
    /// </summary>
    private static bool ProducesGroundForage(FarmAnimal animal) =>
        animal.GetAnimalData()?.HarvestType == FarmAnimalHarvestType.DigUp;

    public bool TryCollect(FarmAnimal animal, ItemBuffer buffer, OutputScopeProvenance? provenance = null)
    {
        if (!HasToolHarvestReady(animal))
            return false;

        var produceId = animal.currentProduce.Value;
        var item = ItemRegistry.Create(produceId, 1) ?? ItemRegistry.Create($"(O){produceId}", 1);
        if (item is null)
            return false;

        if (item is SObject obj)
            obj.Quality = Math.Max(0, animal.produceQuality.Value);

        buffer.Add(item.QualifiedItemId, Math.Max(1, item.Stack), TaskKind.CollectAnimalProducts, provenance);
        animal.HandleStatsOnProduceCollected(item, (uint)Math.Max(1, item.Stack));
        animal.currentProduce.Value = string.Empty;
        animal.daysSinceLastLay.Value = 0;
        return true;
    }

    public static bool IsMilkProduce(FarmAnimal animal)
    {
        var animalType = animal.type.Value;
        return animalType.Contains("Cow", StringComparison.OrdinalIgnoreCase) ||
               animalType.Contains("Goat", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsShearProduce(FarmAnimal animal)
    {
        var animalType = animal.type.Value;
        return animalType.Contains("Sheep", StringComparison.OrdinalIgnoreCase);
    }

    public TileCoord CurrentTile(FarmAnimal animal) =>
        new(animal.TilePoint.X, animal.TilePoint.Y);

    public TileCoord? CurrentNavigationTile(FarmAnimal animal, GameLocation location)
    {
        return CurrentNavigationTiles(animal, location).FirstOrDefaultPassable(location);
    }

    public IReadOnlyList<TileCoord> CurrentNavigationTiles(FarmAnimal animal, GameLocation location)
    {
        var current = CurrentTile(animal);
        var candidates = new List<TileCoord> { current };
        candidates.AddRange(WorkAreaScanner.OrthogonalInteractionTiles(current, location));
        return candidates
            .Where(tile => IsWithinMap(tile, location))
            .Distinct()
            .ToList();
    }

    private static void AddIfSelected(
        FarmAnimal animal,
        IReadOnlySet<string>? selectedHomeLocations,
        List<(AnimalRef Ref, FarmAnimal Live)> result)
    {
        // Match the animal's home against the selection by either its unique interior name (new,
        // precise — services only the selected building) or its type name / building type (legacy
        // contracts that stored the type name still service all of that type).
        if (selectedHomeLocations is not null &&
            !HomeLocationKeys(animal).Any(selectedHomeLocations.Contains))
            return;

        result.Add((
            new AnimalRef(animal.myID.Value, ResolveHomeLocation(animal), animal.displayName),
            animal));
    }

    // The animal's home interior, preferring the live interior reference.
    private static GameLocation? HomeInterior(FarmAnimal animal) =>
        animal.homeInterior ?? animal.home?.indoors.Value;

    // Precise home key (unique per building instance) used as the AnimalRef home + primary match.
    private static string ResolveHomeLocation(FarmAnimal animal) =>
        HomeInterior(animal)?.NameOrUniqueName
        ?? animal.buildingTypeILiveIn.Value
        ?? string.Empty;

    // All identifiers a selection could have used for this animal's home: the unique name (new
    // contracts), the plain type name (legacy contracts / vanilla), and the building type.
    private static IEnumerable<string> HomeLocationKeys(FarmAnimal animal)
    {
        var interior = HomeInterior(animal);
        if (interior is not null)
        {
            yield return interior.NameOrUniqueName;
            if (!string.Equals(interior.Name, interior.NameOrUniqueName, StringComparison.Ordinal))
                yield return interior.Name;
        }

        var typeName = animal.buildingTypeILiveIn.Value;
        if (!string.IsNullOrEmpty(typeName))
            yield return typeName;
    }

    private static FeedWorkPlan EmptyFeedWork(int emptySlots = 0) =>
        new(Array.Empty<WorkItem>(), new TileCoord(0, 0), 0, emptySlots);

    private static int CountPlacedHay(AnimalHouse animalHouse) =>
        animalHouse.numberOfObjectsWithName("Hay");

    private static int FeedCapacity(string? buildingType)
    {
        if (buildingType?.Contains("Deluxe", StringComparison.OrdinalIgnoreCase) == true)
            return 12;

        if (buildingType?.Contains("Big", StringComparison.OrdinalIgnoreCase) == true)
            return 8;

        return 4;
    }

    private static bool IsAutoFeedBuilding(string? buildingType, GameLocation animalHouseLocation)
    {
        if (buildingType?.Contains("Deluxe", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (animalHouseLocation.Map.Properties.TryGetValue("AutoFeed", out var autoFeed))
        {
            var value = autoFeed.ToString();
            return value.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool TryResolveHopperTile(
        GameLocation animalHouseLocation,
        TileCoord? feedStartTile,
        out TileCoord hopperTile,
        out string source)
    {
        foreach (var (tile, obj) in animalHouseLocation.Objects.Pairs)
        {
            if (!IsFeedHopperObject(obj))
                continue;

            hopperTile = new TileCoord((int)tile.X, (int)tile.Y);
            source = "object";
            return true;
        }

        if (TryFindTileAction(animalHouseLocation, IsFeedHopperAction, out hopperTile))
        {
            source = "tile-action";
            return true;
        }

        if (feedStartTile is not null)
        {
            hopperTile = new TileCoord(feedStartTile.Value.X, feedStartTile.Value.Y + 1);
            source = "feed-below-fallback";
            return true;
        }

        hopperTile = default;
        source = "<none>";
        return false;
    }

    private static bool IsFeedHopperObject(SObject obj) =>
        obj.QualifiedItemId.Equals("(BC)99", StringComparison.OrdinalIgnoreCase) ||
        obj.ItemId.Equals("99", StringComparison.OrdinalIgnoreCase) ||
        obj.Name.Contains("Feed Hopper", StringComparison.OrdinalIgnoreCase) ||
        obj.DisplayName.Contains("Feed Hopper", StringComparison.OrdinalIgnoreCase);

    private static bool IsFeedHopperAction(string action) =>
        action.Contains("FeedHopper", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("HayHopper", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("Hopper", StringComparison.OrdinalIgnoreCase);

    private static bool TryFindTileAction(
        GameLocation location,
        Func<string, bool> predicate,
        out TileCoord tile)
    {
        var width = location.Map.Layers.Max(layer => layer.LayerWidth);
        var height = location.Map.Layers.Max(layer => layer.LayerHeight);
        var layers = location.Map.Layers
            .Select(layer => layer.Id)
            .Where(id => id is "Buildings" or "Back" or "Front" or "AlwaysFront")
            .ToArray();

        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            foreach (var layer in layers)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", layer);
                if (!string.IsNullOrWhiteSpace(action) && predicate(action))
                {
                    tile = new TileCoord(x, y);
                    return true;
                }
            }
        }

        tile = default;
        return false;
    }

    private static IEnumerable<TileCoord> ResolveEmptyTroughTiles(GameLocation location)
    {
        var layer = location.Map.Layers[0];
        for (var x = 0; x < layer.LayerWidth; x++)
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            if (location.doesTileHaveProperty(x, y, "Trough", "Back", false) is null)
                continue;

            var tile = new Vector2(x, y);
            if (location.objects.ContainsKey(tile))
                continue;

            yield return new TileCoord(x, y);
        }
    }

    private static TileCoord? FindPreferredFeedStandTile(
        TileCoord feedTile,
        TileCoord hopperTile,
        GameLocation location)
    {
        TileCoord[] candidates =
        {
            new(feedTile.X, feedTile.Y + 1),
            new(feedTile.X + 1, feedTile.Y + 1),
            new(feedTile.X - 1, feedTile.Y + 1),
            new(feedTile.X + 1, feedTile.Y),
            new(feedTile.X - 1, feedTile.Y),
            new(feedTile.X, feedTile.Y - 1),
        };

        return candidates.FirstOrDefaultPassable(location, hopperTile);
    }

    private static TileCoord? FindPreferredStandTile(TileCoord tile, GameLocation location)
    {
        TileCoord[] candidates =
        {
            new(tile.X, tile.Y + 1),
            new(tile.X + 1, tile.Y),
            new(tile.X - 1, tile.Y),
            new(tile.X, tile.Y - 1),
        };

        return candidates.FirstOrDefaultPassable(location);
    }

    private static IReadOnlyList<TileCoord> FeedStandTiles(
        TileCoord feedTile,
        TileCoord hopperTile,
        GameLocation location)
    {
        TileCoord[] candidates =
        {
            new(feedTile.X, feedTile.Y + 1),
            new(feedTile.X + 1, feedTile.Y + 1),
            new(feedTile.X - 1, feedTile.Y + 1),
            new(feedTile.X + 1, feedTile.Y),
            new(feedTile.X - 1, feedTile.Y),
            new(feedTile.X, feedTile.Y - 1),
        };

        return candidates
            .Where(candidate => candidate != hopperTile)
            .Where(candidate => IsWithinMap(candidate, location))
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<TileCoord> StandTilesAround(TileCoord tile, GameLocation location)
    {
        TileCoord[] candidates =
        {
            new(tile.X, tile.Y + 1),
            new(tile.X + 1, tile.Y),
            new(tile.X - 1, tile.Y),
            new(tile.X, tile.Y - 1),
        };

        return candidates
            .Where(candidate => IsWithinMap(candidate, location))
            .Distinct()
            .ToList();
    }

    private static bool IsWithinMap(TileCoord tile, GameLocation location)
    {
        var layer = location.Map.Layers[0];
        return tile.X >= 0 &&
               tile.Y >= 0 &&
               tile.X < layer.LayerWidth &&
               tile.Y < layer.LayerHeight;
    }
}

internal static class TileCoordPassabilityExtensions
{
    public static TileCoord? FirstOrDefaultNullable(this IEnumerable<TileCoord> candidates)
    {
        foreach (var candidate in candidates)
            return candidate;

        return null;
    }

    public static TileCoord? FirstOrDefaultPassable(
        this IEnumerable<TileCoord> candidates,
        GameLocation location,
        TileCoord? blockedTile = null)
    {
        foreach (var candidate in candidates)
        {
            if (blockedTile is not null && candidate == blockedTile.Value)
                continue;

            if (location.Objects.ContainsKey(new Vector2(candidate.X, candidate.Y)))
                continue;

            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(candidate.X, candidate.Y), location))
                return candidate;
        }

        return null;
    }
}
