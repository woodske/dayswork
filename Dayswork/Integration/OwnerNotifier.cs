using StardewValley;

namespace Dayswork.Integration;

/// <summary>
/// Delivers a shift notice to the contract's owner rather than to whoever happens to be at the
/// keyboard. Three cases, in order:
/// <list type="bullet">
/// <item>the owner is the player at this screen → a HUD message, exactly as in single-player;</item>
/// <item>the owner is connected elsewhere → the host sends them the key and its tokens (not the
/// rendered text) so the message reads in their own language;</item>
/// <item>the owner is not connected → the log, and nothing else. Notices are momentary; banking
/// them to replay on their next login would show yesterday's news as today's.</item>
/// </list>
/// </summary>
internal static class OwnerNotifier
{
    /// <summary>Set by the network layer on the host; null in single-player, where there is
    /// nobody to send to.</summary>
    internal static Action<long, string, IReadOnlyDictionary<string, string>, bool>? RemoteSender { get; set; }

    internal static void ShowInfo(long ownerId, string key, IReadOnlyDictionary<string, string>? tokens = null) =>
        Show(ownerId, key, tokens, isError: false);

    internal static void ShowError(long ownerId, string key, IReadOnlyDictionary<string, string>? tokens = null) =>
        Show(ownerId, key, tokens, isError: true);

    internal static void Show(long ownerId, string key, IReadOnlyDictionary<string, string>? tokens, bool isError)
    {
        if (Sponsor.IsLocal(ownerId))
        {
            Game1.addHUDMessage(new HUDMessage(
                Render(key, tokens),
                isError ? HUDMessage.error_type : HUDMessage.newQuest_type));
            return;
        }

        if (Sponsor.IsConnected(ownerId) && RemoteSender is { } send)
        {
            send(ownerId, key, tokens ?? EmptyTokens, isError);
            return;
        }

        ModEntry.ModMonitor.Log(
            $"[Dayswork] Notice for owner {ownerId}: {Render(key, tokens)}",
            DevLog.WarnLevel);
    }

    /// <summary>Renders a notice a remote client received. Same key, that client's language.</summary>
    internal static void ShowReceived(string key, IReadOnlyDictionary<string, string>? tokens, bool isError) =>
        Game1.addHUDMessage(new HUDMessage(
            Render(key, tokens),
            isError ? HUDMessage.error_type : HUDMessage.newQuest_type));

    private static readonly Dictionary<string, string> EmptyTokens = new();

    private static string Render(string key, IReadOnlyDictionary<string, string>? tokens) =>
        tokens is null || tokens.Count == 0
            ? I18nHelper.Get(key)
            : I18nHelper.Get(key, tokens);
}
