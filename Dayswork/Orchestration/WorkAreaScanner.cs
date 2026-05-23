using Dayswork.Core.Capabilities;
using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Dayswork.Orchestration;

internal sealed class WorkAreaScanner
{
    private static readonly HashSet<string> AnimalProductObjectIds = new(StringComparer.Ordinal)
    {
        "107", "(O)107", // Dinosaur Egg
        "174", "(O)174", // Large Egg
        "176", "(O)176", // Egg
        "180", "(O)180", // Egg
        "182", "(O)182", // Large Egg
        "430", "(O)430", // Truffle
        "442", "(O)442", // Duck Egg
        "444", "(O)444", // Duck Feather
        "446", "(O)446", // Rabbit's Foot
    };

    private readonly ICapabilityEvaluator _capability;

    public WorkAreaScanner(ICapabilityEvaluator capability) =>
        _capability = capability;

    public IReadOnlyList<WorkItem> ScanZones(
        GameLocation location,
        IEnumerable<Zone> zones,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
        TileCoord origin)
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
                var tileVec = new Vector2(x, y);
                var taskTile = new TileCoord(x, y);

                var task = DetectTask(tileVec, location, enabled, snapshot,
                    out bool capabilitySkipped, out TaskKind? skippedKind);

                if (capabilitySkipped && skippedKind.HasValue)
                {
                    capabilitySkippedTiles++;
                    ModEntry.ModMonitor.Log($"[Dayswork][scan] capability skip {skippedKind.Value} at {location.Name} ({x},{y}).", LogLevel.Trace);
                }

                if (task is null) continue;

                Increment(detectedByKind, task.Value);
                taskTile = CanonicalTaskTile(task.Value, tileVec, location);
                if (!seenWorkItems.Add((task.Value, taskTile)))
                {
                    duplicateTiles++;
                    continue;
                }

                var navTile = FindNavigationTile(taskTile, task.Value, tileVec, location);
                if (navTile is null)
                {
                    noNavigationTiles++;
                    ModEntry.ModMonitor.Log($"[Dayswork][scan] no stand tile for {task.Value} at {location.Name} task ({taskTile.X},{taskTile.Y}) from scan ({x},{y}).", LogLevel.Trace);
                    continue;
                }

                Increment(acceptedByKind, task.Value);
                rawItems.Add(new WorkItem(navTile.Value, taskTile, task.Value, location.Name));
                ModEntry.ModMonitor.Log($"[Dayswork][scan] accepted {task.Value}: loc={location.Name} nav=({navTile.Value.X},{navTile.Value.Y}) task=({taskTile.X},{taskTile.Y}).", LogLevel.Trace);
            }
        }

        LogWorkScanSummary(location, enabled, zoneCount, scannedTiles, rawItems.Count,
            detectedByKind, acceptedByKind, capabilitySkippedTiles, noNavigationTiles, duplicateTiles);

        return GreedyNearestNeighbour(rawItems, origin);
    }

    public TaskKind? DetectTask(
        Vector2 tileVec,
        GameLocation loc,
        IReadOnlySet<TaskKind> enabled,
        ToolSnapshot snapshot,
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
                    return TaskKind.WaterCrops;

                return null;
            }

            if (tf is FruitTree fruitTree && fruitTree.fruit.Count > 0 &&
                enabled.Contains(TaskKind.CollectFruit))
                return TaskKind.CollectFruit;

            if (tf is Tree && enabled.Contains(TaskKind.CutTrees))
            {
                var axeTarget = ObjectTargetClassifier.ClassifyAxe(tileVec, loc);
                if (axeTarget is null) return null;
                if (!_capability.CanChop(snapshot, axeTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind = TaskKind.CutTrees;
                    return null;
                }
                return TaskKind.CutTrees;
            }
        }

        if (enabled.Contains(TaskKind.CutTrees) &&
            ObjectTargetClassifier.ClassifyAxe(tileVec, loc) is { } clumpAxeTarget &&
            clumpAxeTarget != AxeTarget.FruitTree)
        {
            if (!_capability.CanChop(snapshot, clumpAxeTarget))
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
                    if (!_capability.CanBreak(snapshot, pickTarget.Value))
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
                if (!_capability.CanBreak(snapshot, pickTarget.Value))
                {
                    capabilitySkipped = true;
                    skippedKind = TaskKind.ClearRocks;
                    return null;
                }
                return TaskKind.ClearRocks;
            }
        }

        if (loc.terrainFeatures.TryGetValue(tileVec, out var grassFeature) &&
            grassFeature is Grass &&
            enabled.Contains(TaskKind.ClearGrass))
            return TaskKind.ClearGrass;

        return null;
    }

    public static bool IsAnimalProductForageObject(StardewValley.Object obj) =>
        AnimalProductObjectIds.Contains(obj.QualifiedItemId) ||
        AnimalProductObjectIds.Contains(obj.ItemId);

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
        if (ObjectTargetClassifier.FindResourceClumpAt(tileVec, loc) is { } clump)
            return FindOrthogonalNeighbour(clump, loc);

        if (!RequiresAdjacentNavigation(task) &&
            !IsTrellisCrop(tileVec, loc) &&
            IsTileReachable(taskTile, loc))
            return taskTile;

        return FindOrthogonalNeighbour(taskTile, loc);
    }

    public static bool RequiresAdjacentNavigation(TaskKind task) =>
        task is TaskKind.CollectFruit
             or TaskKind.CutTrees
             or TaskKind.ClearRocks
             or TaskKind.ClearWeeds;

    public static TileCoord? FindOrthogonalNeighbour(TileCoord tile, GameLocation loc)
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
            if (IsTileReachable(c, loc))
                return c;
        }

        return null;
    }

    public static TileCoord? FindOrthogonalNeighbour(ResourceClump clump, GameLocation loc)
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
            if (IsTileReachable(c, loc))
                return c;
        }

        return null;
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

    private static void Increment(Dictionary<TaskKind, int> counts, TaskKind kind) =>
        counts[kind] = counts.TryGetValue(kind, out var existing) ? existing + 1 : 1;

    private static string FormatCounts(Dictionary<TaskKind, int> counts) =>
        counts.Count == 0
            ? "none"
            : string.Join(", ", counts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));

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
        var message =
            $"[Dayswork][scan] location={location.Name} zones={zoneCount} scannedTiles={scannedTiles} " +
            $"enabled={string.Join(",", enabled.OrderBy(t => t))} detected=[{FormatCounts(detectedByKind)}] " +
            $"accepted=[{FormatCounts(acceptedByKind)}] acceptedItems={acceptedItems} " +
            $"capabilitySkipped={capabilitySkippedTiles} noStandTile={noNavigationTiles} duplicateClumpTiles={duplicateTiles}";

        ModEntry.ModMonitor.Log(message, acceptedItems == 0 ? LogLevel.Info : LogLevel.Debug);
    }
}
