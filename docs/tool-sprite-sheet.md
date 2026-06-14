# Tool sprite sheet — `Game1.toolSpriteSheet` (`TileSheets/tools.xnb`)

## Swing-frame Y offsets

Each heavy tool occupies a 64-px band in the sprite sheet. The first band starts at Y=16.

| Tool        | `toolSpriteSheetY` | Confirmed by                        |
|-------------|-------------------|-------------------------------------|
| Hoe         | 16                | Pattern: 80-64 (see note below)     |
| Pickaxe     | 80                | In-game play — confirmed working    |
| Axe         | 144               | In-game play — confirmed working    |
| Watering Can| 208               | In-game play — confirmed working    |

Note: the Hoe value (Y=16) is derived from the consistent 64-px spacing between the three
confirmed tools (Pickaxe 80, Axe 144, WateringCan 208) and verified against the tool
constructor indices (Hoe=47, Pickaxe=131, Axe=215, each 84 apart). Confirm visually in-game
on the first tilling pass and update this note.

## Swing sprite X offsets (used in SpawnHeavyToolSwing)

| Facing direction | X in source rect |
|-----------------|-----------------|
| Down (default)  | 0               |
| Right           | 32              |
| Left            | 32 (flipped)    |
| Up              | 48              |

## Tool inventory icon indices (indexOfMenuItemView from constructor)

Hoe=21, Pickaxe=105, Axe=189 — these are indices in `Game1.toolSpriteSheet` for the 16×16
inventory icon row, not the swing frames.
