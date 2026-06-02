using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.Integration;

/// <summary>
/// The placeable farm building that is the hiring/contract anchor (U-25 WS2). Replaces the bulletin
/// board as the entry point. Interaction and the static output chest are handled in C# (see
/// HiringBuildingInteraction) rather than via Data/Buildings sub-schemas, to keep that logic under
/// our control and reuse the existing chest pipeline.
///
/// Art is a placeholder (assets/hq-building.png); swap the texture file for final art.
/// </summary>
internal static class HiringBuilding
{
    public const string BuildingType = "Bindicle.Dayswork_Office";
    public const string TextureAsset = "Mods/Bindicle.Dayswork/Building";
    private const string TextureFile = "assets/hq-building.png";

    /// <summary>Id of the building's built-in output chest where missed/overflow items are deposited.</summary>
    public const string OutputChestId = "Bindicle.Dayswork_Output";

    // Footprint, in tiles. The placeholder texture is sized to match (3 tiles * 16px = 48px).
    public const int TilesWide = 3;
    public const int TilesHigh = 3;
    public static readonly Point OutputChestDisplayTile = new(2, 1);

    /// <summary>Handles the AssetRequested events that define the building texture and Data/Buildings entry.</summary>
    public static void OnAssetRequested(AssetRequestedEventArgs e, IModHelper helper)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(TextureAsset))
        {
            e.LoadFromModFile<Texture2D>(TextureFile, AssetLoadPriority.Medium);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, BuildingData>().Data;
                data[BuildingType] = BuildData();
            });
        }
    }

    private static BuildingData BuildData() => new()
    {
        Name = I18nHelper.Get("building.office.name"),
        Description = I18nHelper.Get("building.office.description"),
        Texture = TextureAsset,
        Builder = "Robin",
        BuildCost = 5000,
        BuildMaterials = new List<BuildingMaterial>
        {
            new() { ItemId = "(O)388", Amount = 50 }, // Wood
            new() { ItemId = "(O)390", Amount = 50 }, // Stone
        },
        BuildDays = 1,
        Size = new Point(TilesWide, TilesHigh),
        HumanDoor = new Point(1, TilesHigh - 1),
        IndoorMap = null,
        MaxOccupants = 0,
        DrawLayers = new List<BuildingDrawLayer>(),
        Chests = new List<BuildingChest>
        {
            new()
            {
                Id = OutputChestId,
                Type = BuildingChestType.Chest,
                DisplayTile = OutputChestDisplayTile.ToVector2(),
                DisplayHeight = 1f,
            },
        },
    };

    /// <summary>
    /// Resolves the building's built-in output chest, if the building is present on the farm.
    /// Returns null when no building exists (callers must fall back so items are never lost).
    /// </summary>
    public static StardewValley.Objects.Chest? TryGetOutputChest(Farm farm)
    {
        var building = HiringBuildingInteraction.FindHiringBuilding(farm);
        return building?.GetBuildingChest(OutputChestId);
    }
}
