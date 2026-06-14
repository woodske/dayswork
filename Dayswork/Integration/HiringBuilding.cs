using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.Integration;

/// <summary>
/// The placeable farm building that is the hiring/contract anchor. Replaces the bulletin
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
    internal const string OnePerFarmBuildCondition = "!BUILDINGS_CONSTRUCTED All Bindicle.Dayswork_Office 1 2147483647 true";
    private const string TextureFile = "assets/hq-building.png";

    /// <summary>Id of the building's built-in output chest where missed/overflow items are deposited.</summary>
    public const string OutputChestId = "Bindicle.Dayswork_Output";
    /// <summary>Id of the building's built-in input chest where crop-management supplies are stored.</summary>
    public const string InputChestId = "Bindicle.Dayswork_Input";

    /// <summary>
    /// True once the worker has finished and left for the day (set in ShiftOrchestrator.HandleExit,
    /// reset each morning on DayStarted). Drives the evening lit-windows/lantern glow and chimney
    /// smoke, drawn by <see cref="HiringBuildingOverlayRenderer"/>. Single-player only; not persisted.
    /// (Data/Buildings draw layers can't be conditioned in this game version, so the overlay is
    /// rendered in C# instead.)
    /// </summary>
    public static bool WorkCompletedToday { get; set; }

    // Footprint, in tiles, modeled after the Log Cabin. The base building sprite occupies the
    // (0,0,80,106) region of the sheet (5 tiles wide; 106px tall so the roof + chimney overhang
    // well above the 3-tile footprint).
    public const int TilesWide = 5;
    public const int TilesHigh = 3;
    // Output chest on the porch, right of the door (the cabin's mailbox spot) when facing the house.
    public static readonly Point OutputChestDisplayTile = new(3, 2);
    // Input chest on the porch, left of the door when facing the house.
    public static readonly Point InputChestDisplayTile = new(1, 2);

    // ── Spritesheet geometry, shared with HiringBuildingOverlayRenderer ──────────
    // Sheet is 160x122: base (0,0,80,106), window glow (80,0,80,106), smoke row at y=106.
    /// <summary>Pixel size of the base building sprite (the SourceRect region of the sheet).</summary>
    public const int SpriteWidthPx = 80;
    public const int SpriteHeightPx = 106;
    /// <summary>Sheet region of the warm-lit-windows overlay (composited over the base when lit).</summary>
    public static readonly Rectangle GlowSourceRect = new(80, 0, 80, 106);
    /// <summary>First smoke frame on the sheet; frames advance rightward by <see cref="SmokeFramePx"/>.</summary>
    public static readonly Rectangle SmokeFirstFrame = new(0, 106, 16, 16);
    public const int SmokeFramePx = 16;
    public const int SmokeFrameCount = 4;
    public const int SmokeFrameDurationMs = 140;
    /// <summary>Smoke draw offset from the sprite's top-left, in sprite pixels (above the chimney).</summary>
    public static readonly Vector2 SmokeDrawOffset = new(54, -3);
    /// <summary>Earliest time of day (HHMM) at which the lit-window glow is drawn.</summary>
    public const int GlowStartTime = 1800;

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

    internal static BuildingData BuildData() => new()
    {
        Name = I18nHelper.Get("building.office.name"),
        Description = I18nHelper.Get("building.office.description"),
        Texture = TextureAsset,
        Builder = "Robin",
        BuildCondition = OnePerFarmBuildCondition,
        BuildCost = 1000,
        BuildMaterials = new List<BuildingMaterial>
        {
            new() { ItemId = "(O)388", Amount = 25 }, // Wood
            new() { ItemId = "(O)390", Amount = 25 }, // Stone
        },
        BuildDays = 1,
        Size = new Point(TilesWide, TilesHigh),
        // Base building sprite region of the sheet. The glow overlay (x=80) and smoke frames
        // (y=80) live outside this rect; they're drawn conditionally in HiringBuildingOverlayRenderer
        // because BuildingDrawLayer has no GameStateQuery condition in this game version.
        SourceRect = new Rectangle(0, 0, SpriteWidthPx, SpriteHeightPx),
        HumanDoor = new Point(2, TilesHigh - 1), // front-center porch tile (2,2)
        IndoorMap = null,
        MaxOccupants = 0,
        DrawLayers = new List<BuildingDrawLayer>(),
        Chests = BuildChests(),
    };

    internal static List<BuildingChest> BuildChests() =>
        new()
        {
            new()
            {
                Id = InputChestId,
                Type = BuildingChestType.Chest,
                DisplayTile = InputChestDisplayTile.ToVector2(),
                DisplayHeight = 1f,
            },
            new()
            {
                Id = OutputChestId,
                Type = BuildingChestType.Chest,
                DisplayTile = OutputChestDisplayTile.ToVector2(),
                DisplayHeight = 1f,
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

    /// <summary>
    /// Resolves the building's built-in input chest, if the building is present on the farm.
    /// Returns null when no building exists or the chest has not been backfilled yet.
    /// </summary>
    public static StardewValley.Objects.Chest? TryGetInputChest(Farm farm)
    {
        var building = HiringBuildingInteraction.FindHiringBuilding(farm);
        return building?.GetBuildingChest(InputChestId);
    }

    /// <summary>
    /// Absolute farm tile of the building's built-in output chest (the game places the building
    /// chest as a farm object at the display tile). Null when no building exists.
    /// </summary>
    public static Point? TryGetOutputChestTile(Farm? farm)
        => TryGetBuiltInChestTile(farm, OutputChestDisplayTile);

    /// <summary>
    /// Absolute farm tile of the building's built-in input chest. Null when no building exists.
    /// Used to exclude the supply chest from selectable output destinations.
    /// </summary>
    public static Point? TryGetInputChestTile(Farm? farm)
        => TryGetBuiltInChestTile(farm, InputChestDisplayTile);

    internal static bool IsInputChestDisplayTile(int localX, int localY) =>
        InputChestDisplayTile.X == localX && InputChestDisplayTile.Y == localY;

    internal static bool IsOutputChestDisplayTile(int localX, int localY) =>
        OutputChestDisplayTile.X == localX && OutputChestDisplayTile.Y == localY;

    private static Point? TryGetBuiltInChestTile(Farm? farm, Point displayTile)
    {
        if (farm is null)
            return null;

        var building = HiringBuildingInteraction.FindHiringBuilding(farm);
        if (building is null)
            return null;

        return new Point(
            building.tileX.Value + displayTile.X,
            building.tileY.Value + displayTile.Y);
    }
}
