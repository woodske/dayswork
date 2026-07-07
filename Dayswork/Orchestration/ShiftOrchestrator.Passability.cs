using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>
/// Keeps the per-shift <see cref="LocationPassabilityCache"/> in step with the world. Worker-cleared
/// resource clumps are invalidated at the clear site (no SMAPI event exists for them); everything
/// else the worker or player adds/removes rides these SMAPI world-list events. All handlers no-op
/// when no shift is active. Staleness is safe by design (see <see cref="LocationPassabilityCache"/>),
/// so these only need to be approximately right — they re-probe the affected tiles rather than
/// tracking exact deltas.
/// </summary>
internal sealed partial class ShiftOrchestrator
{
    public void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (_session is null) return;
        foreach (var kv in e.Added)   Session.Passability.InvalidateTile(e.Location, (int)kv.Key.X, (int)kv.Key.Y);
        foreach (var kv in e.Removed) Session.Passability.InvalidateTile(e.Location, (int)kv.Key.X, (int)kv.Key.Y);
    }

    public void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
    {
        if (_session is null) return;
        foreach (var kv in e.Added)   Session.Passability.InvalidateTile(e.Location, (int)kv.Key.X, (int)kv.Key.Y);
        foreach (var kv in e.Removed) Session.Passability.InvalidateTile(e.Location, (int)kv.Key.X, (int)kv.Key.Y);
    }

    public void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
    {
        if (_session is null) return;
        foreach (var f in e.Added)   InvalidatePixelFootprint(e.Location, f.GetBoundingBox());
        foreach (var f in e.Removed) InvalidatePixelFootprint(e.Location, f.GetBoundingBox());
    }

    public void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (_session is null) return;
        foreach (var b in e.Added)
            Session.Passability.InvalidateArea(e.Location, b.tileX.Value, b.tileY.Value, b.tilesWide.Value, b.tilesHigh.Value);
        foreach (var b in e.Removed)
            Session.Passability.InvalidateArea(e.Location, b.tileX.Value, b.tileY.Value, b.tilesWide.Value, b.tilesHigh.Value);
    }

    private void InvalidatePixelFootprint(GameLocation location, Rectangle pixelBounds)
    {
        var left = pixelBounds.Left / 64;
        var top = pixelBounds.Top / 64;
        var width = Math.Max(1, (pixelBounds.Width + 63) / 64);
        var height = Math.Max(1, (pixelBounds.Height + 63) / 64);
        Session.Passability.InvalidateArea(location, left, top, width, height);
    }
}
