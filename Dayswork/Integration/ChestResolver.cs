using Dayswork.Core.Compat;
using Dayswork.Core.Domain;
using Dayswork.UI;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Dayswork.Integration;

// Integration bridge between Core ChestRef and live Stardew game state.
// Singleton — constructed once in ModEntry. All methods query game state fresh on each call.
internal sealed class ChestResolver
{
    private readonly IModHelper _helper;

    internal ChestResolver(IModHelper helper) => _helper = helper;

    // Returns all accessible chests on the farm and in buildings.
    // Called once when ZoneAndChestMenu opens — never per frame.
    internal List<ChestEntry> GetAllChests(GameLocation farm, IReadOnlyList<GreenhouseSelection>? selectedGreenhouses = null)
    {
        var result = new List<ChestEntry>();
        string farmGroup = I18nHelper.Get("ui.zone_chest.group_farm");

        // Built-in office chests are reserved system chests. The input chest is a crop-supply
        // reservoir; the output chest remains the automatic fallback, but is not an explicit
        // selectable destination.
        var officeInputChestTile = HiringBuilding.TryGetInputChestTile(farm as Farm);
        var officeOutputChestTile = HiringBuilding.TryGetOutputChestTile(farm as Farm);

        // Open-farm chests
        foreach (var (tile, obj) in farm.Objects.Pairs)
        {
            if (obj is Chest chest)
            {
                var tileX = (int)tile.X;
                var tileY = (int)tile.Y;
                if (ShouldExcludeSelectableFarmChest(officeInputChestTile, officeOutputChestTile, tileX, tileY))
                    continue;
                var chestRef = new ChestRef(farm.Name, new TileCoord(tileX, tileY));
                result.Add(new ChestEntry(chestRef, GetDisplayName(chest, farm, tileX, tileY), farmGroup));
            }
        }

        // Building-interior chests
        if (farm is Farm f)
        {
            foreach (var building in f.buildings)
            {
                if (!BuildingLocationResolver.TryGetInteriorForBuilding(building, out var indoors))
                    continue;

                string buildingName = building.buildingType.Value;
                foreach (var (tile, obj) in indoors.Objects.Pairs)
                {
                    if (obj is Chest chest)
                    {
                        var tileX = (int)tile.X;
                        var tileY = (int)tile.Y;
                        var chestRef = new ChestRef(indoors.Name, new TileCoord(tileX, tileY));
                        result.Add(new ChestEntry(chestRef, GetDisplayName(chest, indoors, tileX, tileY), buildingName));
                    }
                }
            }
        }

        if (farm is Farm expansionFarm && ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (selectedGreenhouses is null ||
                    !selectedGreenhouses.Any(greenhouse => compat.IsExpansionChestVisibleForScope(descriptor, greenhouse)))
                    continue;

                if (!compat.TryValidateRoute(
                        expansionFarm,
                        "Farm",
                        descriptor.LocationName,
                        ExpansionRoutePurpose.DepositEntry,
                        out _,
                        out _))
                    continue;

                var location = Game1.getLocationFromName(descriptor.LocationName);
                if (location is null)
                    continue;

                foreach (var (tile, obj) in location.Objects.Pairs)
                {
                    if (obj is not Chest chest)
                        continue;

                    var tileX = (int)tile.X;
                    var tileY = (int)tile.Y;
                    var chestRef = new ChestRef(location.NameOrUniqueName, new TileCoord(tileX, tileY));
                    result.Add(new ChestEntry(
                        chestRef,
                        GetDisplayName(chest, location, tileX, tileY),
                        descriptor.DisplayName));
                }
            }
        }

        return result;
    }

    // Groups all accessible chests into one map-view location each (only locations with ≥1 chest),
    // for the map-based chest picker. DisplayName/GroupLabel come from GetAllChests.
    internal List<ChestMapLocation> BuildChestMapLocations(GameLocation farm, IReadOnlyList<GreenhouseSelection>? selectedGreenhouses = null) =>
        ChestMapLocation.Group(GetAllChests(farm, selectedGreenhouses));

    // Human-readable label for a stored ChestRef, used by summary screens. Resolves the live chest
    // for its name/building; if the chest was moved or destroyed, falls back to coordinates.
    internal static string DescribeChestRef(ChestRef chestRef)
    {
        var location = Game1.getLocationFromName(chestRef.LocationName);
        if (location is not null
            && location.Objects.TryGetValue(new Vector2(chestRef.Tile.X, chestRef.Tile.Y), out var obj)
            && obj is Chest chest)
        {
            if (!string.IsNullOrWhiteSpace(chest.Name) && chest.Name != "Chest")
                return chest.Name;

            return I18nHelper.Get("ui.zone_chest.chest_fallback_name",
                new { buildingName = location.Name, x = chestRef.Tile.X, y = chestRef.Tile.Y });
        }

        return I18nHelper.Get("ui.zone_chest.chest_missing",
            new { x = chestRef.Tile.X, y = chestRef.Tile.Y });
    }

    // Resolves a stored ChestRef to a live Chest. Returns null if the chest was moved or destroyed.
    internal Chest? ResolveChest(ChestRef chestRef)
    {
        var location = Game1.getLocationFromName(chestRef.LocationName);
        if (location == null) return null;

        var tile = new Vector2(chestRef.Tile.X, chestRef.Tile.Y);
        return location.Objects.TryGetValue(tile, out var obj) && obj is Chest chest ? chest : null;
    }

    // Generates i18n-aware display name.
    // Uses chest.Name if set by the player; otherwise falls back to "{building} — Chest at {x}, {y}".
    internal string GetDisplayName(Chest chest, GameLocation location, int tileX, int tileY)
    {
        if (!string.IsNullOrWhiteSpace(chest.Name) && chest.Name != "Chest")
            return chest.Name;

        return I18nHelper.Get("ui.zone_chest.chest_fallback_name",
            new { buildingName = location.Name, x = tileX, y = tileY });
    }

    // Returns building footprint outlines for ZoneDrawOverlay and building-select mode.
    internal List<BuildingOutline> GetBuildingOutlines(Farm farm)
    {
        var result = new List<BuildingOutline>();
        foreach (var building in farm.buildings)
        {
            if (!BuildingLocationResolver.TryGetInteriorForBuilding(building, out var indoors))
                continue;

            // Key the selection by the interior's UNIQUE name (distinct per building instance) so two
            // same-type buildings (two Coops/Barns) are distinct selections and each is serviced.
            // DisplayName stays the building type for premium-tier classification + friendly display.
            //
            result.Add(new BuildingOutline(
                indoors.NameOrUniqueName,
                new Rectangle(building.tileX.Value, building.tileY.Value,
                               building.tilesWide.Value, building.tilesHigh.Value),
                building.buildingType.Value));
        }

        if (ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (!descriptor.IsWorkScopeEligible)
                    continue;

                if (!compat.TryValidateRoute(
                        farm,
                        "Farm",
                        descriptor.LocationName,
                        ExpansionRoutePurpose.WorkEntry,
                        out var route,
                        out _))
                    continue;

                var firstApproach = route.Hops[0].Hop.ApproachTile;
                result.Add(new BuildingOutline(
                    descriptor.LocationName,
                    new Rectangle(firstApproach.X - 1, firstApproach.Y - 1, 3, 3),
                    descriptor.DisplayName));
            }
        }

        return result;
    }

    internal static bool ShouldExcludeSelectableFarmChest(
        Point? officeInputChestTile,
        Point? officeOutputChestTile,
        int tileX,
        int tileY) =>
        ShouldExcludeSelectableFarmChest(
            officeInputChestTile?.X,
            officeInputChestTile?.Y,
            officeOutputChestTile?.X,
            officeOutputChestTile?.Y,
            tileX,
            tileY);

    internal static bool ShouldExcludeSelectableFarmChest(int? officeInputChestTileX, int? officeInputChestTileY, int tileX, int tileY) =>
        ShouldExcludeSelectableFarmChest(officeInputChestTileX, officeInputChestTileY, null, null, tileX, tileY);

    internal static bool ShouldExcludeSelectableFarmChest(
        int? officeInputChestTileX,
        int? officeInputChestTileY,
        int? officeOutputChestTileX,
        int? officeOutputChestTileY,
        int tileX,
        int tileY) =>
        officeInputChestTileX.HasValue
        && officeInputChestTileY.HasValue
        && tileX == officeInputChestTileX.Value
        && tileY == officeInputChestTileY.Value
        || officeOutputChestTileX.HasValue
        && officeOutputChestTileY.HasValue
        && tileX == officeOutputChestTileX.Value
        && tileY == officeOutputChestTileY.Value;
}
