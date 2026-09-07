using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using Microsoft.Xna.Framework.Graphics;

namespace Dayswork.Integration;

/// <summary>
/// The placeable farm building that is the hiring/contract anchor. Replaces the bulletin
/// board as the entry point. Interaction and the static output chest are handled in C# (see
/// HiringBuildingInteraction) rather than via Data/Buildings sub-schemas, to keep that logic under
/// our control and reuse the existing chest pipeline.
///
/// </summary>
internal static class HiringBuilding
{
    public const string BuildingType = "Bindicle.Dayswork_Office";
    public const string TextureAsset = "Mods/Bindicle.Dayswork/Building";
    private const string TextureFile = "assets/farmhand_office.png";

    /// <summary>Id of the building's built-in output chest where missed/overflow items are deposited.</summary>
    public const string OutputChestId = "Bindicle.Dayswork_Output";
    /// <summary>Id of the building's built-in input chest where crop-management supplies are stored.</summary>
    public const string InputChestId = "Bindicle.Dayswork_Input";

    // Footprint, in tiles, modeled after the Log Cabin. The base building sprite occupies the
    // (0,0,80,106) region of the sheet (5 tiles wide; 106px tall so the roof + chimney overhang
    // well above the 3-tile footprint).
    public const int TilesWide = 5;
    public const int TilesHigh = 3;
    // Output chest on the porch, right of the door (the mailbox spot) when facing the house.
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
        // No build condition: 2.0 allows any number of offices, one farmhand each.
        BuildCost = 1000,
        BuildMaterials = new List<BuildingMaterial>
        {
            new() { ItemId = GameItemIds.Wood,  Amount = 25 },
            new() { ItemId = GameItemIds.Stone, Amount = 25 },
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
    /// One office's built-in output chest — the overflow sink that keeps hard rule 4 (items are
    /// never lost). Null when the office has been demolished, in which case callers fall back to
    /// the shipping bin. Always ask for a specific office: "the farm's office" stopped being a
    /// meaningful question in 2.0.
    /// </summary>
    public static StardewValley.Objects.Chest? TryGetOutputChest(Building? office) =>
        office?.GetBuildingChest(OutputChestId);

    /// <summary>
    /// One office's built-in input chest (the managed-crop supply reservoir). Null when the office
    /// is gone or the chest has not been backfilled yet.
    /// </summary>
    public static StardewValley.Objects.Chest? TryGetInputChest(Building? office) =>
        office?.GetBuildingChest(InputChestId);

    internal static bool IsInputChestDisplayTile(int localX, int localY) =>
        InputChestDisplayTile.X == localX && InputChestDisplayTile.Y == localY;

    internal static bool IsOutputChestDisplayTile(int localX, int localY) =>
        OutputChestDisplayTile.X == localX && OutputChestDisplayTile.Y == localY;
}
