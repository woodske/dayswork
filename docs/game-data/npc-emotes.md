# NPC emote icon indices

`Character.doEmote(int emoteId)` takes an icon-index from the emote spritesheet.
These constants are defined as `static int` fields on `StardewValley.Character`.

Verified via reflection on **Stardew Valley.dll v1.6.15.24356**.
Canonical source in the mod: `Dayswork/Worker/Emotes.cs`.

| Constant (Character field)  | Value | Description                        |
|-----------------------------|-------|------------------------------------|
| `emptyCanEmote`             | 4     | Watering-can-empty bubble          |
| `questionMarkEmote`         | 8     | "?" confused                       |
| `angryEmote`                | 12    | Steam/anger cloud                  |
| `exclamationEmote`          | 16    | "!" surprised                      |
| `heartEmote`                | 20    | Heart                              |
| `sleepEmote`                | 24    | "Zzz"                              |
| `sadEmote`                  | 28    | Sad face                           |
| `happyEmote`                | 32    | Happy/smile face                   |
| `xEmote`                    | 36    | "X" / no                           |
| `pauseEmote`                | 40    | "…" pause/ellipsis                 |
| `videoGameEmote`            | 52    | Game controller                    |
| `musicNoteEmote`            | 56    | Music note                         |
| `blushEmote`                | 60    | Blush (hidden from player menu)    |

## Farmer.EMOTES (player emote menu)

The player's emote menu uses `Farmer.EMOTES`, an array of `EmoteType` objects that also
reference the icon indices above (the `emoteIconIndex` field). Multiple named emotes can
share the same icon — they differ in animation frames. NPC emotes only use the index, so
only the `Character` constants above matter for `doEmote`.

| Menu index | emoteIconIndex | emoteString  | Hidden |
|------------|----------------|--------------|--------|
| 0          | 32             | happy        |        |
| 1          | 28             | sad          |        |
| 2          | 20             | heart        |        |
| 3          | 16             | exclamation  |        |
| 4          | 56             | note         |        |
| 5          | 24             | sleep        |        |
| 6          | 52             | game         |        |
| 7          | 8              | question     |        |
| 8          | 36             | x            |        |
| 9          | 40             | pause        |        |
| 10         | 60             | blush        | yes    |
| 11         | 12             | angry        |        |
| 12         | 56             | yes          |        |
| 13         | 36             | no           |        |
| 14         | 12             | sick         |        |
| 15         | 56             | laugh        |        |
| 16         | 16             | surprised    |        |
| 17         | 56             | hi           |        |
| 18         | 12             | taunt        | yes    |
| 19         | 40             | uh           |        |
| 20         | 56             | music        | yes    |
| 21         | -1             | jar          | yes    |
