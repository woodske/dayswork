namespace Dayswork.Worker;

/// <summary>
/// Emote icon-index constants for <see cref="StardewValley.Character.doEmote"/>.
/// Verified via reflection on Stardew Valley.dll v1.6.15.24356 — mirror of Character's
/// static int fields (emptyCanEmote, questionMarkEmote, …).
/// </summary>
internal static class Emotes
{
    internal const int EmptyCan   = 4;  // watering can empty bubble
    internal const int Question   = 8;  // "?" confused
    internal const int Angry      = 12; // steam/anger cloud
    internal const int Exclamation = 16; // "!" surprised
    internal const int Heart      = 20; // heart
    internal const int Sleep      = 24; // "Zzz"
    internal const int Sad        = 28; // sad face
    internal const int Happy      = 32; // happy/smile face
    internal const int X          = 36; // "X" / no
    internal const int Pause      = 40; // "..." pause/ellipsis
    internal const int VideoGame  = 52; // game controller
    internal const int MusicNote  = 56; // music note
    internal const int Blush      = 60; // blush (hidden from player emote menu)
}
