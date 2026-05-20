# Tech Stack Decisions — U-11 Full Hiring UI: Zones & Chests

## Summary
No new technology is introduced in U-11. All tech stack choices were made in prior units. This document confirms the applicable decisions.

---

## Confirmed Decisions (from prior units)

| Decision | Choice | Decided In |
|---|---|---|
| UI framework | SMAPI `IClickableMenu` subclassing | U-09 |
| Game/SMAPI version | SV 1.6.x + SMAPI 4.x + .NET 6 | Q2 / U-01 |
| Rendering API | `SpriteBatch` (XNA/MonoGame) via SMAPI events | U-01 |
| World overlay event | `helper.Events.Display.RenderedWorld` | NFR-PERF-03 / NFR-UX-03 enforcement |
| i18n system | SMAPI `helper.Translation` via `I18nHelper` (M-21) | U-08 |
| Dependency injection | Constructor injection throughout Mod layer | U-09 / NFR-MAINT-03 |
| Test framework | xUnit + FsCheck | U-02 |
| Core/Mod separation | `Dayswork.Core` has zero SMAPI/SV references | Application Design D1 |

## No New NuGet Packages
U-11 introduces no new NuGet dependencies. The existing packages (SMAPI, StardewValley, Pathoschild.Stardew.ModBuildConfig, Harmony) cover all required APIs.

## No New Required Mod Dependencies
U-11 introduces no new required or optional mod dependencies. Mail Framework Mod (MFM) is added in U-14; GMCM in U-16.

---

## Key API Usage Notes (for Code Generation reference)

### SpriteBatch pixel-fill pattern (NFR-PERF-03)
Zone tile highlights are rendered as single filled rectangles, not per-tile draws:
```csharp
// Create a 1×1 white pixel texture once (stored as _pixelTexture):
var tex = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
tex.SetData(new[] { Color.White });

// In RenderedWorld handler, per zone:
var screenRect = Game1.GlobalToLocal(Game1.viewport, zone.ScreenBounds);
e.SpriteBatch.Draw(_pixelTexture, screenRect, Color.LightBlue * 0.4f);
```

### Display.RenderedWorld lifecycle pattern (NFR-ONBOARD-01)
```csharp
// Constructor:
helper.Events.Display.RenderedWorld += OnRenderedWorld;

// cleanupBeforeExit():
ModEntry.Instance.Helper.Events.Display.RenderedWorld -= OnRenderedWorld;
```

### leftClickHeld pattern for drag selection (NFR-ONBOARD-01)
```csharp
public override void receiveLeftClick(int x, int y, bool playSound = true)
{
    if (_inZoneDrawMode) { _dragStart = new Point(x, y); return; }
    // ... normal button handling
}

public override void leftClickHeld(int x, int y)
{
    if (_inZoneDrawMode) { _dragCurrent = new Point(x, y); /* update preview */ }
}

public override void releaseLeftClick(int x, int y)
{
    if (_inZoneDrawMode) { FinalizeZone(_dragStart, new Point(x, y)); _inZoneDrawMode = false; }
}
```
