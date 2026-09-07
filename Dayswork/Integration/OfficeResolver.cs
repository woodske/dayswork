using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Integration;

/// <summary>
/// Finds farmhand offices. Replaces the 1.x <c>FindHiringBuilding</c>, which returned the farm's
/// first office and so silently picked the wrong one the moment a second existed: every caller now
/// says *which* office it means, by id (the contract's <c>OfficeId</c>) or by the tile the player
/// clicked.
/// </summary>
internal static class OfficeResolver
{
    /// <summary>Every office on the farm, in the farm's own building order.</summary>
    internal static IEnumerable<Building> EnumerateOffices(Farm? farm)
    {
        if (farm is null)
            yield break;

        foreach (var building in farm.buildings)
        {
            if (IsOffice(building))
                yield return building;
        }
    }

    internal static IReadOnlyList<Building> ListOffices(Farm? farm) =>
        EnumerateOffices(farm).ToList();

    /// <summary>The office with this <c>Building.id</c>, or null when it has been demolished.</summary>
    internal static Building? TryGet(Farm? farm, Guid officeId)
    {
        if (officeId == Guid.Empty)
            return null;

        foreach (var building in EnumerateOffices(farm))
        {
            if (building.id.Value == officeId)
                return building;
        }

        return null;
    }

    /// <summary>Convenience overload against the live farm.</summary>
    internal static Building? TryGet(Guid officeId) => TryGet(Game1.getFarm(), officeId);

    /// <summary>The office whose footprint covers this farm tile — what the player clicked.</summary>
    internal static Building? AtTile(Farm? farm, int x, int y)
    {
        foreach (var building in EnumerateOffices(farm))
        {
            if (FootprintContains(building, x, y))
                return building;
        }

        return null;
    }

    internal static bool IsOffice(Building building) =>
        string.Equals(building.buildingType.Value, HiringBuilding.BuildingType, StringComparison.Ordinal);

    internal static bool FootprintContains(Building building, int x, int y) =>
        x >= building.tileX.Value
        && x < building.tileX.Value + building.tilesWide.Value
        && y >= building.tileY.Value
        && y < building.tileY.Value + building.tilesHigh.Value;

    /// <summary>Absolute farm tile of one of the office's porch chests (the game places a building
    /// chest as a farm object at its display tile).</summary>
    internal static Point ChestTile(Building office, Point displayTile) =>
        new(office.tileX.Value + displayTile.X, office.tileY.Value + displayTile.Y);
}
