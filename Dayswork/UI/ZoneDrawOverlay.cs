using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.UI;

// Pure renderer — draws zone highlights and tile grid on the farm via Display.RenderedWorld.
// Subscribed/unsubscribed by ZoneDrawMenu (cleanupBeforeExit).
// Reads IZoneDrawSource; holds no mutable state.
internal sealed class ZoneDrawOverlay : IDisposable
{
    private static readonly Color DragPreviewFillColor = new(144, 238, 144, 255);

    private readonly IZoneDrawSource _source;
    private Texture2D? _pixelTexture;
    private bool _disposed;

    internal ZoneDrawOverlay(IZoneDrawSource source, GraphicsDevice graphicsDevice)
    {
        _source = source;
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    // Fires after the world is drawn, before the UI layer.
    // SpriteBatch is already begun — draw directly, no Begin/End needed (NFR-ONBOARD-01).
    internal void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (_disposed || _pixelTexture == null) return;

        // Tile grid — rendered beneath zone fills; only shown while actively drawing (user requested)
        if (_source.IsInZoneDrawMode)
            DrawTileGrid(e.SpriteBatch);

        // Completed zones — semi-transparent fill in the layer's color (O(zone count), not O(tile count) — NFR-PERF-03)
        foreach (var zone in _source.CompletedZones)
            DrawZoneFill(e.SpriteBatch, zone.TopLeft, zone.BottomRight, _source.ZoneFillColor);

        // Protected zones belong to other managed-crop groups and cannot be selected in this session.
        foreach (var zone in _source.ProtectedZones)
            DrawZoneFill(e.SpriteBatch, zone.TopLeft, zone.BottomRight, _source.ProtectedZoneFillColor);

        // Selected building footprints — yellow fill
        foreach (var outline in _source.SelectedBuildings)
        {
            var screenRect = TileBoundsToScreen(outline.TileBounds);
            e.SpriteBatch.Draw(_pixelTexture, screenRect, Color.Yellow * 0.35f);
        }

        // In-progress drag preview
        if (_source.IsInZoneDrawMode && _source.DragStart.HasValue)
        {
            var start   = _source.DragStart.Value;
            var current = _source.DragCurrent;
            var topLeft     = new TileCoord(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y));
            var bottomRight = new TileCoord(Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
            DrawZoneFill(e.SpriteBatch, topLeft, bottomRight, DragPreviewFillColor * 0.35f);
        }
    }

    // Renders thin 1-px grid lines over the visible viewport so the player can see tile boundaries.
    private void DrawTileGrid(SpriteBatch sb)
    {
        int startTileX = Game1.viewport.X / Game1.tileSize;
        int startTileY = Game1.viewport.Y / Game1.tileSize;
        int endTileX   = (Game1.viewport.X + Game1.viewport.Width)  / Game1.tileSize + 2;
        int endTileY   = (Game1.viewport.Y + Game1.viewport.Height) / Game1.tileSize + 2;

        var color = Color.White * 0.15f;

        for (int tx = startTileX; tx <= endTileX; tx++)
        {
            int sx = tx * Game1.tileSize - Game1.viewport.X;
            int sy = startTileY * Game1.tileSize - Game1.viewport.Y;
            int lh = (endTileY - startTileY + 1) * Game1.tileSize;
            sb.Draw(_pixelTexture, new Rectangle(sx, sy, 1, lh), color);
        }

        for (int ty = startTileY; ty <= endTileY; ty++)
        {
            int sx = startTileX * Game1.tileSize - Game1.viewport.X;
            int sy = ty * Game1.tileSize - Game1.viewport.Y;
            int lw = (endTileX - startTileX + 1) * Game1.tileSize;
            sb.Draw(_pixelTexture, new Rectangle(sx, sy, lw, 1), color);
        }
    }

    private void DrawZoneFill(SpriteBatch sb, TileCoord topLeft, TileCoord bottomRight, Color color)
    {
        // World pixel → screen: subtract viewport top-left. SpriteBatch matrix handles zoom.
        int screenX = topLeft.X     * Game1.tileSize - Game1.viewport.X;
        int screenY = topLeft.Y     * Game1.tileSize - Game1.viewport.Y;
        int w       = (bottomRight.X - topLeft.X + 1) * Game1.tileSize;
        int h       = (bottomRight.Y - topLeft.Y + 1) * Game1.tileSize;
        sb.Draw(_pixelTexture, new Rectangle(screenX, screenY, w, h), color);
    }

    private static Rectangle TileBoundsToScreen(Rectangle tileBounds)
    {
        int screenX = tileBounds.X      * Game1.tileSize - Game1.viewport.X;
        int screenY = tileBounds.Y      * Game1.tileSize - Game1.viewport.Y;
        int w       = tileBounds.Width  * Game1.tileSize;
        int h       = tileBounds.Height * Game1.tileSize;
        return new Rectangle(screenX, screenY, w, h);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pixelTexture?.Dispose();
        _pixelTexture = null;
    }
}
