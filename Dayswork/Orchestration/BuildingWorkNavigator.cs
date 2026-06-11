using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Orchestration;

// Resolves building door/entry/exit tiles. All actual movement (walking to a door, warping
// through it) is the TravelRunner's job; this class never moves the worker.
internal sealed class BuildingWorkNavigator
{
    private readonly IMonitor _monitor;

    public BuildingWorkNavigator(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public bool TryResolveDoorTile(
        string interiorLocationName,
        out TileCoord outdoorDoorTile,
        out GameLocation interior)
    {
        if (BuildingLocationResolver.TryResolve(Game1.getFarm(), interiorLocationName, out var match))
        {
            outdoorDoorTile = match.OutdoorDoorTile;
            interior = match.Interior;
            return true;
        }

        outdoorDoorTile = new TileCoord(0, 0);
        interior = null!;
        LogSkipped(interiorLocationName);
        return false;
    }

    public TileCoord ResolveInteriorEntryTile(GameLocation interior)
    {
        var warp = interior.warps.FirstOrDefault();
        return warp is null
            ? new TileCoord(0, 0)
            : new TileCoord(warp.X, warp.Y);
    }

    public IReadOnlyList<TileCoord> ResolveInteriorExitApproachTiles(GameLocation interior)
    {
        var warp = ResolveInteriorEntryTile(interior);
        TileCoord[] candidates =
        {
            new(warp.X, warp.Y - 1),
            new(warp.X - 1, warp.Y),
            new(warp.X + 1, warp.Y),
            new(warp.X, warp.Y + 1),
            warp,
        };

        var result = new List<TileCoord>();
        foreach (var candidate in candidates)
        {
            if ((candidate == warp ||
                 WorkerMovementDriver.IsTilePassableForWorker(new Point(candidate.X, candidate.Y), interior)) &&
                !result.Contains(candidate))
            {
                result.Add(candidate);
            }
        }

        return result.Count > 0 ? result : new[] { warp };
    }

    internal static TileCoord SelectNearestReachableExitApproachTile(
        IEnumerable<TileCoord> candidates,
        IReadOnlyDictionary<TileCoord, int> routeCosts,
        TileCoord fallback)
    {
        var bestTile = fallback;
        var bestCost = int.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!routeCosts.TryGetValue(candidate, out var cost) || cost >= bestCost)
                continue;

            bestTile = candidate;
            bestCost = cost;
        }

        return bestTile;
    }

    public void LogSkipped(string interiorLocationName)
    {
        _monitor.Log(
            I18nHelper.Get("log.building.skipped", new { location = interiorLocationName }),
            DevLog.WarnLevel);
        _monitor.Log(
            BuildingLocationResolver.DescribeResolutionState(Game1.getFarm(), interiorLocationName),
            DevLog.WarnLevel);
    }

    public bool TryResolveInterior(string interiorLocationName, out GameLocation interior)
    {
        if (BuildingLocationResolver.TryGetInterior(Game1.getFarm(), interiorLocationName, out interior))
        {
            return true;
        }

        interior = null!;
        return false;
    }
}
