using StardewModdingAPI;

namespace Dayswork;

/// <summary>
/// One switch for all verbose developer logging and dev-only tooling (the debug console commands
/// and the player tile-step logger, gated in <see cref="ModEntry"/>). Routing dev logs through here
/// means we never have to comment/uncomment individual log lines while iterating.
///
/// ⚠️ RELEASE CHECKLIST: set <see cref="Enabled"/> to <c>false</c> before shipping — it silences the
/// verbose internal traces and unregisters the debug commands. Leave <c>true</c> while developing.
///
/// (Field, not <c>const</c>, on purpose: a <c>const</c> would make <c>if (Enabled)</c> bodies
/// unreachable when false and trip TreatWarningsAsErrors. This stays a one-line flip.)
/// </summary>
internal static class DevLog
{
    // ⚠️⚠️ DEV TOGGLE — SET TO false BEFORE RELEASE ⚠️⚠️
    internal static bool Enabled = true;

    /// <summary>
    /// Writes a verbose developer message when <see cref="Enabled"/>; a no-op otherwise. Defaults to
    /// <see cref="LogLevel.Trace"/> (log file always; console only in SMAPI developer mode). Bump the
    /// default to <see cref="LogLevel.Debug"/> if you want dev logs in the console without dev mode.
    /// </summary>
    internal static void Log(string message, LogLevel level = LogLevel.Trace)
    {
        if (Enabled)
            ModEntry.ModMonitor?.Log(message, level);
    }
}
