using Dayswork.Core.Domain;
using Dayswork.Worker;
using Microsoft.Xna.Framework;
using System.Text;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Integration;

internal sealed record BuildingLocationMatch(
    Building? Building,
    GameLocation Interior,
    TileCoord OutdoorDoorTile,
    string DisplayName);

internal static class BuildingLocationResolver
{
    public static string DescribeResolutionState(Farm farm, string requestedName)
    {
        var sb = new StringBuilder();
        var standalone = Game1.getLocationFromName(requestedName);
        sb.Append("[Dayswork][building-resolve] requested='")
            .Append(requestedName)
            .Append("' farm='")
            .Append(farm.Name)
            .Append("' buildings=")
            .Append(farm.buildings.Count)
            .Append(" warps=")
            .Append(farm.warps.Count)
            .Append(" standalone=")
            .Append(standalone?.Name ?? "<null>");

        var index = 0;
        foreach (var building in farm.buildings)
        {
            var indoorsName = Safe(() => building.GetIndoorsName());
            var indoors = SafeLocation(() => building.indoors.Value)
                          ?? SafeLocation(() => building.GetIndoors())
                          ?? (!string.IsNullOrWhiteSpace(indoorsName)
                              ? SafeLocation(() => Game1.getLocationFromName(indoorsName))
                              : null);
            var door = SafePoint(() => building.getPointForHumanDoor());
            TileCoord? approach = door is null ? null : ResolveOutdoorApproachTile(farm, door.Value);

            sb.Append(" | #")
                .Append(index++)
                .Append(" type='")
                .Append(building.buildingType.Value)
                .Append("' indoorsName='")
                .Append(indoorsName ?? "<null>")
                .Append("' indoors='")
                .Append(indoors?.Name ?? "<null>")
                .Append("' tile=(")
                .Append(building.tileX.Value)
                .Append(',')
                .Append(building.tileY.Value)
                .Append(") size=(")
                .Append(building.tilesWide.Value)
                .Append('x')
                .Append(building.tilesHigh.Value)
                .Append(") door=(")
                .Append(door?.X.ToString() ?? "?")
                .Append(',')
                .Append(door?.Y.ToString() ?? "?")
                .Append(") approach=(")
                .Append(approach?.X.ToString() ?? "?")
                .Append(',')
                .Append(approach?.Y.ToString() ?? "?")
                .Append(") matches=")
                .Append(Matches(requestedName, building, indoors));
        }

        return sb.ToString();
    }

    public static string NormalizeLocationName(Farm farm, string requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName) ||
            string.Equals(requestedName, "Farm", StringComparison.OrdinalIgnoreCase))
            return "Farm";

        return TryResolve(farm, requestedName, out var match)
            ? match.Interior.Name
            : requestedName;
    }

    public static bool TryResolve(Farm farm, string requestedName, out BuildingLocationMatch match)
    {
        foreach (var building in farm.buildings)
        {
            TryGetInteriorForBuilding(building, requestedName, out var interior);
            if (!Matches(requestedName, building, interior))
                continue;

            interior ??= ResolveInteriorByName(requestedName, building);
            if (interior is null)
                continue;

            var door = building.getPointForHumanDoor();
            match = new BuildingLocationMatch(
                building,
                interior,
                ResolveOutdoorApproachTile(farm, door),
                building.buildingType.Value);
            return true;
        }

        var standalone = Game1.getLocationFromName(requestedName);
        if (standalone is not null && TryFindFarmWarpTo(farm, standalone.Name, out var warpTile))
        {
            match = new BuildingLocationMatch(null, standalone, warpTile, standalone.Name);
            return true;
        }

        match = null!;
        return false;
    }

    public static bool TryGetInterior(Farm farm, string requestedName, out GameLocation interior)
    {
        if (TryResolve(farm, requestedName, out var match))
        {
            interior = match.Interior;
            return true;
        }

        interior = null!;
        return false;
    }

    public static bool TryGetInteriorForBuilding(Building building, out GameLocation interior) =>
        TryGetInteriorForBuilding(building, null, out interior);

    public static bool TryGetInteriorForBuilding(Building building, string? requestedName, out GameLocation interior)
    {
        var resolved = building.indoors.Value
                       ?? building.GetIndoors()
                       ?? Game1.getLocationFromName(building.GetIndoorsName())
                       ?? Game1.getLocationFromName(building.buildingType.Value)
                       ?? (requestedName is null ? null : Game1.getLocationFromName(requestedName));

        if (resolved is not null)
        {
            interior = resolved;
            return true;
        }

        interior = null!;
        return false;
    }

    private static GameLocation? ResolveInteriorByName(string requestedName, Building building) =>
        building.indoors.Value
        ?? building.GetIndoors()
        ?? Game1.getLocationFromName(building.GetIndoorsName())
        ?? Game1.getLocationFromName(requestedName)
        ?? Game1.getLocationFromName(building.buildingType.Value);

    private static bool Matches(string requestedName, Building building, GameLocation? interior)
    {
        if (NameEquals(interior?.Name, requestedName) ||
            NameEquals(building.GetIndoorsName(), requestedName) ||
            NameEquals(building.buildingType.Value, requestedName))
            return true;

        return LooseBuildingTypeMatch(requestedName, building.buildingType.Value);
    }

    private static bool LooseBuildingTypeMatch(string requestedName, string buildingType)
    {
        if (string.IsNullOrWhiteSpace(requestedName) || string.IsNullOrWhiteSpace(buildingType))
            return false;

        return requestedName.Contains(buildingType, StringComparison.OrdinalIgnoreCase) ||
               buildingType.Contains(requestedName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool NameEquals(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool TryFindFarmWarpTo(Farm farm, string targetName, out TileCoord warpTile)
    {
        foreach (var warp in farm.warps)
        {
            if (!string.Equals(warp.TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                continue;

            warpTile = new TileCoord(warp.X, warp.Y);
            return true;
        }

        warpTile = new TileCoord(0, 0);
        return false;
    }

    private static TileCoord ResolveOutdoorApproachTile(Farm farm, Point door)
    {
        TileCoord[] candidates =
        {
            new(door.X, door.Y + 1),
            new(door.X - 1, door.Y + 1),
            new(door.X + 1, door.Y + 1),
            new(door.X, door.Y),
            new(door.X - 1, door.Y),
            new(door.X + 1, door.Y),
            new(door.X, door.Y - 1),
        };

        foreach (var candidate in candidates)
        {
            if (WorkerMovementDriver.IsTilePassableForWorker(new Point(candidate.X, candidate.Y), farm))
                return candidate;
        }

        return new TileCoord(door.X, door.Y);
    }

    private static string? Safe(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static GameLocation? SafeLocation(Func<GameLocation?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static Point? SafePoint(Func<Point> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
