using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Diagnostics;

internal sealed class PlayerTileStepLogger
{
    private readonly IMonitor _monitor;

    private bool _enabled;
    private bool _hasLastPosition;
    private Point _lastTile;
    private string _lastLocationName = "";

    public PlayerTileStepLogger(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public bool IsEnabled => _enabled;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        _enabled = enabled;

        if (_enabled)
        {
            SnapshotCurrentPosition();
            _monitor.Log("[Dayswork][debug] Player tile step logger enabled.", LogLevel.Info);
            return;
        }

        Reset();
        _monitor.Log("[Dayswork][debug] Player tile step logger disabled.", LogLevel.Info);
    }

    public void Toggle() => SetEnabled(!_enabled);

    public void Reset()
    {
        _hasLastPosition = false;
        _lastTile = default;
        _lastLocationName = "";
    }

    public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!_enabled || !Context.IsWorldReady || Game1.player is null)
            return;

        var currentTile = Game1.player.TilePoint;
        var currentLocationName = Game1.player.currentLocation?.NameOrUniqueName ?? "";
        if (!_hasLastPosition)
        {
            _lastTile = currentTile;
            _lastLocationName = currentLocationName;
            _hasLastPosition = true;
            return;
        }

        if (currentTile == _lastTile && string.Equals(currentLocationName, _lastLocationName, StringComparison.Ordinal))
            return;

        _lastTile = currentTile;
        _lastLocationName = currentLocationName;
        _monitor.Log(
            $"[Dayswork][debug] Player tile step: {currentLocationName} ({currentTile.X}, {currentTile.Y})",
            LogLevel.Debug);
    }

    private void SnapshotCurrentPosition()
    {
        Reset();

        if (!Context.IsWorldReady || Game1.player is null)
            return;

        _lastTile = Game1.player.TilePoint;
        _lastLocationName = Game1.player.currentLocation?.NameOrUniqueName ?? "";
        _hasLastPosition = true;
    }
}
