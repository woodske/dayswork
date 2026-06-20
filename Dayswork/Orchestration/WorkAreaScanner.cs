using Dayswork.Core.Capabilities;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

internal sealed class WorkAreaScanner
{
    // Legacy explicit ids retained as a guaranteed-parity INCLUDE set (covers Truffle 430, whose
    // category we don't hardcode, plus the original vanilla products). Open-ended new content —
    // vanilla or SVE — is matched by category instead.
    private static readonly HashSet<string> AnimalProductObjectIds = new(StringComparer.Ordinal)
    {
        "107", "(O)107", // Dinosaur Egg
        "174", "(O)174", // Large Egg
        "176", "(O)176", // Egg
        "180", "(O)180", // Egg
        "182", "(O)182", // Large Egg
        "289", "(O)289", // Ostrich Egg
        "305", "(O)305", // Void Egg
        "928", "(O)928", // Golden Egg
        "430", "(O)430", // Truffle
        "442", "(O)442", // Duck Egg
        "444", "(O)444", // Duck Feather
        "446", "(O)446", // Rabbit's Foot
    };

    // Animal-product item categories that drop on the ground and should be collected. Covers
    // vanilla Egg (-5) and Animal Goods (-18: Wool/Duck Feather/Rabbit's Foot) AND SVE products that
    // share them (Goose Egg/Golden Goose Egg -5; Camel Wool -18) AND any future product. Milk (-6)
    // is intentionally excluded — it is tool-harvested, never a ground object.
    private static readonly HashSet<int> AnimalProductCategories = new()
    {
        StardewValley.Object.EggCategory,             // -5
        StardewValley.Object.sellAtPierresAndMarnies, // -18 (Wool, Duck Feather, Rabbit's Foot, Camel Wool)
    };

    // Narrow safety valve for a verified category false positive (a ground object in an animal-product
    // category that should NOT be auto-collected). Empty unless a real case is found.
    private static readonly HashSet<string> AnimalProductExcludedIds = new(StringComparer.Ordinal);

    private static bool IsInAnyZone(int x, int y, IReadOnlyList<Zone> zones)
    {
        foreach (var zone in zones)
        {
            if (x >= zone.TopLeft.X && x <= zone.BottomRight.X &&
                y >= zone.TopLeft.Y && y <= zone.BottomRight.Y)
                return true;
        }

        return false;
    }

    public IReadOnlyList<WorkItem> ScanZones(
        GameLocation location,
        IEnumerable<Zone> zones,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
        TileCoord origin,
        ContractPreferences preferences,
        OutputScopeProvenance? provenance = null,
        IReadOnlyList<Zone>? excludedZones = null)
    {
        var seenWorkItems = new HashSet<(TaskKind Task, TileCoord Tile)>();
        var rawItems = new List<WorkItem>();
        var detectedByKind = new Dictionary<TaskKind, int>();
        var acceptedByKind = new Dictionary<TaskKind, int>();
        var scannedTiles = 0;
        var capabilitySkippedTiles = 0;
        var duplicateTiles = 0;
        var noNavigationTiles = 0;
        var zoneCount = 0;

        foreach (var zone in zones)
        {
            zoneCount++;

            for (var x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
            for (var y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
            {
                scannedTiles++;

                // Coexistence: a tile owned by a managed crop zone is serviced
                // by the managed-crop path, so the general Water/Harvest/clearing scan skips it.
                if (excludedZones is not null && IsInAnyZone(x, y, excludedZones))
                    continue;

                var tileVec = new Vector2(x, y);
                var taskTile = new TileCoord(x, y);

                var task = DetectTask(tileVec, location, enabled, snapshot, preferences,
                    out bool capabilitySkipped, out TaskKind? skippedKind);

                if (capabilitySkipped && skippedKind.HasValue)
                {
                    capabilitySkippedTiles++;
//                     ModEntry.ModMonitor.Log($"[Dayswork][scan] capability skip {skippedKind.Value} at {location.Name} ({x},{y}).", LogLevel.Trace);
                }

                if (task is null) continue;

                Increment(detectedByKind, task.Value);
                taskTile = CanonicalTaskTile(task.Value, tileVec, location);
                if (!seenWorkItems.Add((task.Value, taskTile)))
                {
                    duplicateTiles++;
                    continue;
                }

                var navTiles = FindNavigationTiles(taskTile, task.Value, tileVec, location).ToList();
                if (navTiles.Count == 0)
                {
                    noNavigationTiles++;
//                     ModEntry.ModMonitor.Log($"[Dayswork][scan] no stand tile for {task.Value} at {location.Name} task ({taskTile.X},{taskTile.Y}) from scan ({x},{y}).", LogLevel.Trace);
                    continue;
                }

                Increment(acceptedByKind, task.Value);
                rawItems.Add(new WorkItem(navTiles[0], taskTile, task.Value, location.Name, provenance, navTiles));
//                 ModEntry.ModMonitor.Log($"[Dayswork][scan] accepted {task.Value}: loc={location.Name} nav=({navTiles[0].X},{navTiles[0].Y}) task=({taskTile.X},{taskTile.Y}) candidates=[{string.Join("; ", navTiles.Select(tile => $"({tile.X},{tile.Y})"))}].", LogLevel.Trace);

                // Harvest leaves regrowable crops in place. Queue a separate WaterCrops beat
                // for the same tile so the worker plays a visible watering-can animation
                // instead of silently re-wetting the dirt as a side effect of harvest.
                if (task.Value == TaskKind.HarvestCrops
                    && enabled.Contains(TaskKind.WaterCrops)
                    && location.terrainFeatures.TryGetValue(tileVec, out var followUpTf)
                    && followUpTf is HoeDirt followUpDirt
                    && followUpDirt.crop is not null
                    && followUpDirt.crop.RegrowsAfterHarvest()
                    && followUpDirt.state.Value != HoeDirt.watered)
                {
                    if (WontRegrowBeforeSeasonEnd(followUpDirt.crop, location))
                        CropHudNotifier.WaterSkippedSeasonEnd();
                    else if (seenWorkItems.Add((TaskKind.WaterCrops, taskTile)))
                    {
                        Increment(detectedByKind, TaskKind.WaterCrops);
                        Increment(acceptedByKind, TaskKind.WaterCrops);
                        rawItems.Add(new WorkItem(navTiles[0], taskTile, TaskKind.WaterCrops, location.Name, provenance, navTiles));
//                         ModEntry.ModMonitor.Log($"[Dayswork][scan] accepted WaterCrops (post-harvest regrow): loc={location.Name} nav=({navTiles[0].X},{navTiles[0].Y}) task=({taskTile.X},{taskTile.Y}).", LogLevel.Trace);
                    }
                }
            }
        }

        LogWorkScanSummary(location, enabled, zoneCount, scannedTiles, rawItems.Count,
            detectedByKind, acceptedByKind, capabilitySkippedTiles, noNavigationTiles, duplicateTiles);

        return rawItems;
    }

    public IReadOnlyList<WorkItem> ScanWholeLocation(
        GameLocation location,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
        TileCoord origin,
        ContractPreferences preferences,
        OutputScopeProvenance? provenance = null)
    {
        var layer = location.Map.Layers[0];
        var wholeLocation = new Zone(
            location.Name,
            new TileCoord(0, 0),
            new TileCoord(Math.Max(0, layer.LayerWidth - 1), Math.Max(0, layer.LayerHeight - 1)));

        return ScanZones(location, new[] { wholeLocation }, enabled, snapshot, origin, preferences, provenance);
    }

    public TaskKind? DetectTask(
        Vector2 tileVec,
        GameLocation loc,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
        ContractPreferences preferences,
        out bool capabilitySkipped,
        out TaskKind? skippedKind)
    {
        capabilitySkipped = false;
        skippedKind = null;

        if (loc.terrainFeatures.TryGetValue(tileVec, out var tf))
        {
            if (tf is HoeDirt dirt && dirt.crop is not null)
            {
                if (enabled.Contains(TaskKind.HarvestCrops) &&
                    dirt.readyForHarvest())
                    return TaskKind.HarvestCrops;

                if (enabled.Contains(TaskKind.WaterCrops) &&
                    dirt.state.Value != HoeDirt.watered &&
                    !dirt.crop.dead.Value &&
                    !dirt.readyForHarvest())
                {
                    if (WontMatureBeforeSeasonEnd(dirt.crop, loc))
                    {
                        CropHudNotifier.WaterSkippedSeasonEnd();
                        return null;
                    }
                    return TaskKind.WaterCrops;
                }

                return null;
            }

            if (tf is FruitTree fruitTree && fruitTree.fruit.Count > 0 &&
                enabled.Contains(TaskKind.CollectFruit))
                return TaskKind.CollectFruit;

            if (tf is Tree && enabled.Contains(TaskKind.CutTrees))
            {
                var axeTarget = ObjectTargetClassifier.ClassifyAxe(tileVec, loc);
                if (axeTarget is null) return null;
                if (!CapabilityMatrix.CanChop(snapshot.AxeLevel, axeTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind = TaskKind.CutTrees;
                    return null;
                }
                if (ObjectTargetClassifier.HasTapper(tileVec, loc)) return null;
                return TaskKind.CutTrees;
            }
        }

        if (enabled.Contains(TaskKind.CutTrees) &&
            ObjectTargetClassifier.ClassifyAxe(tileVec, loc) is { } clumpAxeTarget &&
            clumpAxeTarget != AxeTarget.FruitTree)
        {
            if (!CapabilityMatrix.CanChop(snapshot.AxeLevel, clumpAxeTarget))
            {
                capabilitySkipped = true;
                skippedKind = TaskKind.CutTrees;
                return null;
            }

            return TaskKind.CutTrees;
        }

        if (loc.objects.TryGetValue(tileVec, out var obj))
        {
            if (enabled.Contains(TaskKind.CollectAnimalProducts) && IsAnimalProductForageObject(obj))
                return TaskKind.CollectAnimalProducts;

            if (obj.IsWeeds() && enabled.Contains(TaskKind.ClearWeeds))
                return TaskKind.ClearWeeds;

            if (enabled.Contains(TaskKind.ClearRocks))
            {
                var pickTarget = ObjectTargetClassifier.ClassifyPick(tileVec, loc);
                if (pickTarget is not null)
                {
                    if (!CapabilityMatrix.CanBreak(snapshot.PickaxeLevel, pickTarget.Value))
                    {
                        capabilitySkipped = true;
                        skippedKind = TaskKind.ClearRocks;
                        return null;
                    }
                    return TaskKind.ClearRocks;
                }
            }

            if (obj.Name == "Twig" && enabled.Contains(TaskKind.CutTrees))
                return TaskKind.CutTrees;
        }

        if (enabled.Contains(TaskKind.ClearRocks) && !loc.objects.ContainsKey(tileVec))
        {
            var pickTarget = ObjectTargetClassifier.ClassifyPick(tileVec, loc);
            if (pickTarget is not null)
            {
                if (!CapabilityMatrix.CanBreak(snapshot.PickaxeLevel, pickTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind = TaskKind.ClearRocks;
                    return null;
                }
                return TaskKind.ClearRocks;
            }
        }

        if (loc.terrainFeatures.TryGetValue(tileVec, out var grassFeature) &&
            grassFeature is Grass grass &&
            enabled.Contains(TaskKind.ClearGrass) &&
            !(preferences.AvoidBlueGrass && grass.grassType.Value == Grass.blueGrass))
            return TaskKind.ClearGrass;

        return null;
    }

    public static bool IsAnimalProductForageObject(StardewValley.Object obj) =>
        IsAnimalProductForageObject(obj.QualifiedItemId, obj.ItemId, obj.Category, obj.bigCraftable.Value);

    /// <summary>
    /// Pure animal-product test (SMAPI-free, for unit testing). True when the object is a ground
    /// animal product: a guaranteed-parity legacy id, or an item in an animal-product category
    /// (Egg -5 / Animal Goods -18) that is not a big-craftable and not explicitly excluded.
    /// </summary>
    internal static bool IsAnimalProductForageObject(string qualifiedItemId, string itemId, int category, bool bigCraftable)
    {
        if (AnimalProductExcludedIds.Contains(qualifiedItemId) ||
            AnimalProductExcludedIds.Contains(itemId))
            return false;

        return AnimalProductObjectIds.Contains(qualifiedItemId) ||
               AnimalProductObjectIds.Contains(itemId) ||
               (!bigCraftable && AnimalProductCategories.Contains(category));
    }

    public static bool IsTrellisCrop(Vector2 tileVec, GameLocation loc)
    {
        if (!loc.terrainFeatures.TryGetValue(tileVec, out var tf) || tf is not HoeDirt dirt)
            return false;
        return dirt.crop?.raisedSeeds.Value == true;
    }

    public static TileCoord CanonicalTaskTile(TaskKind task, Vector2 tileVec, GameLocation loc)
    {
        if (task is TaskKind.CutTrees or TaskKind.ClearRocks &&
            ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
            return new TileCoord((int)clump.Tile.X, (int)clump.Tile.Y);

        return new TileCoord((int)tileVec.X, (int)tileVec.Y);
    }

    public static TileCoord? FindNavigationTile(TileCoord taskTile, TaskKind task, Vector2 tileVec, GameLocation loc)
    {
        foreach (var tile in FindNavigationTiles(taskTile, task, tileVec, loc))
        {
            if (IsTileReachable(tile, loc))
                return tile;
        }

        return null;
    }

    public static IReadOnlyList<TileCoord> FindNavigationTiles(TileCoord taskTile, TaskKind task, Vector2 tileVec, GameLocation loc)
    {
        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
            return OrthogonalInteractionTiles(clump, loc);

        if (!RequiresAdjacentNavigation(task) &&
            !IsTrellisCrop(tileVec, loc) &&
            IsWithinMap(taskTile, loc))
            return new[] { taskTile };

        return OrthogonalInteractionTiles(taskTile, loc);
    }

    public static bool RequiresAdjacentNavigation(TaskKind task) =>
        task is TaskKind.CollectFruit
             or TaskKind.CollectAnimalProducts
             or TaskKind.CutTrees
             or TaskKind.ClearRocks
             or TaskKind.ClearWeeds;

    public static TileCoord? FindOrthogonalNeighbour(TileCoord tile, GameLocation loc)
    {
        foreach (var candidate in FindOrthogonalNeighbours(tile, loc))
            return candidate;

        return null;
    }

    public static IEnumerable<TileCoord> FindOrthogonalNeighbours(TileCoord tile, GameLocation loc)
    {
        TileCoord[] candidates =
        {
            new(tile.X, tile.Y - 1),
            new(tile.X + 1, tile.Y),
            new(tile.X, tile.Y + 1),
            new(tile.X - 1, tile.Y),
        };

        foreach (var c in candidates)
        {
            if (IsWithinMap(c, loc) && IsTileReachable(c, loc))
                yield return c;
        }
    }

    public static TileCoord? FindOrthogonalNeighbour(ResourceClump clump, GameLocation loc)
    {
        foreach (var candidate in FindOrthogonalNeighbours(clump, loc))
            return candidate;

        return null;
    }

    public static IEnumerable<TileCoord> FindOrthogonalNeighbours(ResourceClump clump, GameLocation loc)
    {
        var minX = (int)clump.Tile.X;
        var minY = (int)clump.Tile.Y;
        var maxX = minX + clump.width.Value - 1;
        var maxY = minY + clump.height.Value - 1;

        var candidates = new List<TileCoord>();
        for (var x = minX; x <= maxX; x++)
        {
            candidates.Add(new TileCoord(x, minY - 1));
            candidates.Add(new TileCoord(x, maxY + 1));
        }

        for (var y = minY; y <= maxY; y++)
        {
            candidates.Add(new TileCoord(minX - 1, y));
            candidates.Add(new TileCoord(maxX + 1, y));
        }

        foreach (var c in candidates.Distinct())
        {
            if (IsWithinMap(c, loc) && IsTileReachable(c, loc))
                yield return c;
        }
    }

    public static IReadOnlyList<TileCoord> OrthogonalInteractionTiles(TileCoord tile, GameLocation loc)
    {
        TileCoord[] candidates =
        {
            new(tile.X, tile.Y - 1),
            new(tile.X + 1, tile.Y),
            new(tile.X, tile.Y + 1),
            new(tile.X - 1, tile.Y),
        };

        return candidates.Where(candidate => IsWithinMap(candidate, loc)).ToList();
    }

    public static IReadOnlyList<TileCoord> OrthogonalInteractionTiles(ResourceClump clump, GameLocation loc)
    {
        var minX = (int)clump.Tile.X;
        var minY = (int)clump.Tile.Y;
        var maxX = minX + clump.width.Value - 1;
        var maxY = minY + clump.height.Value - 1;

        var candidates = new List<TileCoord>();
        for (var x = minX; x <= maxX; x++)
        {
            candidates.Add(new TileCoord(x, minY - 1));
            candidates.Add(new TileCoord(x, maxY + 1));
        }

        for (var y = minY; y <= maxY; y++)
        {
            candidates.Add(new TileCoord(minX - 1, y));
            candidates.Add(new TileCoord(maxX + 1, y));
        }

        return candidates
            .Distinct()
            .Where(candidate => IsWithinMap(candidate, loc))
            .ToList();
    }

    public static List<WorkItem> GreedyNearestNeighbour(List<WorkItem> items, TileCoord origin)
    {
        if (items.Count == 0) return items;

        var remaining = new List<WorkItem>(items);
        var sorted = new List<WorkItem>(items.Count);
        var current = origin;

        while (remaining.Count > 0)
        {
            int bestIdx = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                int dist = Math.Abs(remaining[i].TaskTile.X - current.X)
                         + Math.Abs(remaining[i].TaskTile.Y - current.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            var chosen = remaining[bestIdx];
            remaining.RemoveAt(bestIdx);
            sorted.Add(chosen);
            current = chosen.NavTile;
        }

        return sorted;
    }

    private static bool IsTileReachable(TileCoord tile, GameLocation loc) =>
        WorkerMovementDriver.IsTilePassableForWorker(new Point(tile.X, tile.Y), loc);

    private static bool IsWithinMap(TileCoord tile, GameLocation loc)
    {
        var layer = loc.Map.Layers[0];
        return tile.X >= 0 &&
               tile.Y >= 0 &&
               tile.X < layer.LayerWidth &&
               tile.Y < layer.LayerHeight;
    }

    private static void Increment(Dictionary<TaskKind, int> counts, TaskKind kind) =>
        counts[kind] = counts.TryGetValue(kind, out var existing) ? existing + 1 : 1;

    private static string FormatCounts(Dictionary<TaskKind, int> counts) =>
        counts.Count == 0
            ? "none"
            : string.Join(", ", counts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));

    private static bool WontMatureBeforeSeasonEnd(StardewValley.Crop crop, GameLocation loc)
    {
        if (loc.SeedsIgnoreSeasonsHere())
            return false;

        var daysLeft = 28 - Game1.dayOfMonth;
        var daysRemaining = CropDaysRemaining(crop);
        if (daysRemaining <= daysLeft)
            return false;

        var nextSeason = (StardewValley.Season)(((int)Game1.season + 1) % 4);
        var data = crop.GetData();
        if (data?.Seasons?.Contains(nextSeason) ?? true)
            return false;

        return true;
    }

    private static bool WontRegrowBeforeSeasonEnd(StardewValley.Crop crop, GameLocation loc)
    {
        if (loc.SeedsIgnoreSeasonsHere())
            return false;

        var data = crop.GetData();
        var regrowDays = data?.RegrowDays ?? -1;
        if (regrowDays <= 0)
            return false;

        var daysLeft = 28 - Game1.dayOfMonth;
        if (regrowDays <= daysLeft)
            return false;

        var nextSeason = (StardewValley.Season)(((int)Game1.season + 1) % 4);
        if (data?.Seasons?.Contains(nextSeason) ?? true)
            return false;

        return true;
    }

    private static int CropDaysRemaining(StardewValley.Crop crop)
    {
        if (crop.fullyGrown.Value)
            return crop.dayOfCurrentPhase.Value;

        var remaining = 0;
        var phaseDays = crop.phaseDays;
        var phase = crop.currentPhase.Value;
        var dayInPhase = crop.dayOfCurrentPhase.Value;
        for (var i = phase; i < phaseDays.Count - 1; i++)
            remaining += i == phase ? phaseDays[i] - dayInPhase : phaseDays[i];
        return remaining;
    }

    private static void LogWorkScanSummary(
        GameLocation location,
        IReadOnlySet<TaskKind> enabled,
        int zoneCount,
        int scannedTiles,
        int acceptedItems,
        Dictionary<TaskKind, int> detectedByKind,
        Dictionary<TaskKind, int> acceptedByKind,
        int capabilitySkippedTiles,
        int noNavigationTiles,
        int duplicateTiles)
    {
        DevLog.Log(
            $"[Dayswork][scan] location={location.Name} zones={zoneCount} scannedTiles={scannedTiles} " +
            $"enabled={string.Join(",", enabled.OrderBy(t => t))} detected=[{FormatCounts(detectedByKind)}] " +
            $"accepted=[{FormatCounts(acceptedByKind)}] acceptedItems={acceptedItems} " +
            $"capabilitySkipped={capabilitySkippedTiles} noStandTile={noNavigationTiles} duplicateClumpTiles={duplicateTiles}");
    }
}
