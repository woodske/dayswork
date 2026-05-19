using StardewModdingAPI;

namespace Dayswork.Integration;

internal static class I18nHelper
{
    private static IModHelper _helper = null!;

    internal static void Init(IModHelper helper) => _helper = helper;

    internal static string Get(string key) =>
        _helper.Translation.Get(key).ToString();
}
