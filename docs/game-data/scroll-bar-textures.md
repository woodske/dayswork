# Scroll bar textures

Used by `Dayswork/UI/MenuScrollBar.cs` to draw the custom scroll bar in all hiring menus.

## Source

On PC, `MenuScrollBar` loads these at runtime via reflection from `ShopMenu.ShopCachedTheme` (a
nested type that exists in the PC build of SDV 1.6 but not Android). The `DevLog.Enabled` path
logs the actual values — **run once with DevLog.Enabled to confirm the fallback below matches**.

## Fallback (Android / ShopCachedTheme absent)

All four elements use `Game1.mouseCursors` (`LooseSprites/Cursors`):

| Element        | Source rect (x, y, w, h) | Scaled size (×4) | Notes |
|----------------|--------------------------|------------------|-------|
| Up arrow       | 421, 459, 11, 12         | 44 × 48          | Standard IClickableMenu value; high confidence |
| Down arrow     | 421, 472, 11, 12         | 44 × 48          | Standard IClickableMenu value; high confidence |
| Track (back)   | 403, 383, 6, 6           | 24 × 24 (tiled)  | Confirmed via DevLog 2026-06-13 |
| Thumb (front)  | 435, 463, 6, 10          | 24 × 40 (tiled)  | Confirmed via DevLog 2026-06-13 |
