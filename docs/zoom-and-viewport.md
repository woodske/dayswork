# Zoom & viewport (verified against SDV 1.6.15)

How the camera/zoom work for the menus that show a frozen, panned view of the farm
(`Dayswork/UI/ZoneDrawMenu.cs` + `ZoneDrawOverlay.cs`). Confirmed by decompiling
`Stardew Valley.dll` (ilspycmd) on 2026-06-13.

## The view is the game's own world render

`ZoneDrawMenu` sets `Game1.viewportFreeze = true`, swaps `Game1.currentLocation`, and pans by
writing `Game1.viewport.X/Y` directly. The farm tiles/buildings are the **game's** render;
`ZoneDrawOverlay` only draws grid/zone fills, into the `Display.RenderedWorld` SpriteBatch — which
is already scaled by the game's zoom matrix. So the overlay automatically lines up with the tiles.

## Zoom = the game's zoom level

| Member | Type | Meaning |
|---|---|---|
| `Options.minZoom` / `Options.maxZoom` | `const float` | 0.75 / 2.0 — valid base-zoom range |
| `Game1.options.zoomLevel` | get-only prop | what the world render uses; returns `baseZoomLevel` |
| `Game1.options.baseZoomLevel` | field | applied base zoom |
| `Game1.options.desiredBaseZoomLevel` | get/set prop | **the setter** — target zoom (single-player backing field is `singlePlayerBaseZoomLevel`, XML key `zoomLevel`) |

**To change zoom, set `Game1.options.desiredBaseZoomLevel`.** Every frame `Game1.update` does:
`if (desiredBaseZoomLevel != baseZoomLevel) { baseZoomLevel = desiredBaseZoomLevel;
forceSnapOnNextViewportUpdate = true; refreshWindowSettings(); }`. `refreshWindowSettings()` →
`updateViewportForScreenSizeChange`, which recomputes `Game1.viewport.Width/Height` as
`ceil(screen / zoomLevel)` and re-centers on the current view center. UI scale (`desiredUIScale`)
is independent, so menu buttons/mouse don't rescale.

Consequences (why the zoom feature is a small change):
- **Hit-testing needs no zoom math.** `Game1.getMouseX(false)` is defined as
  `mouseState.X * (1f / options.zoomLevel)`, so `CursorTile()` already maps correctly at any zoom.
- **Rendering needs no zoom math.** The overlay draws in world-pixel space relative to
  `Game1.viewport`; the matrix scales it and `viewport.Width/Height` adapt, so the grid range and
  `PanViewport` clamps stay correct.
- Save `desiredBaseZoomLevel` on menu open and restore on `cleanupBeforeExit` so the player's normal
  zoom is preserved.
- **Do NOT change `desiredUIScale` when zooming.** `PushUIMode` sets `uiViewport.Width` =
  `viewport.Width * zoomLevel / uiScale` = `screen.Width / uiScale` — independent of `zoomLevel`.
  Changing only `desiredBaseZoomLevel` leaves button bounds valid. Changing `desiredUIScale`
  shifts `uiViewport.Width`, making stored button bounds stale and breaking hit testing.

## Android zoom — `desiredBaseZoomLevel` is read-only on Android (verified 2026-06-13)

On Android, `Game1.options.desiredBaseZoomLevel`'s **getter returns the device's hardware DPI zoom
(e.g. 1.969) regardless of what the setter writes**. Confirmed by adding diagnostic logging: after
writing `desiredBaseZoomLevel = 1.719`, reading it back on the next frame returned 1.969.

Consequence: the game's update loop (`if (desired != base) { base = desired; refresh(); }`) reverts
`baseZoomLevel` to the hardware zoom every frame, undoing any zoom change.

**Workaround**: track the intended zoom in `_effectiveZoom`; in `update()` (which runs inside
`Game1.update()` after the revert), force `Game1.options.baseZoomLevel = _effectiveZoom` and call
`Game1.game1.refreshWindowSettings()` if they differ. The draw phase runs after `update()` and sees
the forced value. Use `baseZoomLevel` (not `desiredBaseZoomLevel`) to save and restore zoom on open/close.

## Multitouch / pinch (Android)

`Microsoft.Xna.Framework.Input.Touch.TouchPanel.GetState()` exists in the bundled
`MonoGame.Framework.dll` and returns a `TouchCollection` (indexable; `[i].Position` is a `Vector2`).
On PC it returns 0 points (so polling it is inert); on Android it returns the live touch points.
Two-finger contact (touches.Count >= 2) confirmed working on-device. `ZoneDrawMenu` also exposes
+/− buttons as a fallback. The game's own `Game1.panMode` is an unrelated debug feature, not this.
