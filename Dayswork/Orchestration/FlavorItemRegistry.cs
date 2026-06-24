using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// Per-shift capture-and-clone store for <b>flavored / colored</b> output items — roe, aged roe,
/// wine/juice/jelly/pickles, flavored honey, etc. — whose identity (<c>PreserveId</c>,
/// <c>PreserveType</c>, color) and sell price cannot be reconstructed from a qualified item id alone
/// (e.g. "Sturgeon Roe" and plain "Roe" are both <c>(O)812</c>).
///
/// At collect time <see cref="Register"/> stores the real item as a template under a stable token; the
/// pure deposit buffer carries only that token. At deposit time <see cref="Rebuild"/> clones the
/// template (which <c>getOne()</c> copies flavor/color/price from), so the player receives the exact
/// flavored item. Plain items return a null token and use the ordinary id-based path.
///
/// Lives on <see cref="ShiftSession"/> (a fresh instance per shift); never persisted.
/// </summary>
internal sealed class FlavorItemRegistry
{
    private readonly Dictionary<string, SObject> _templates = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, SObject> Templates => _templates;

    /// <summary>
    /// Returns a stable flavor token when <paramref name="item"/> carries flavor/color a qualified-id
    /// rebuild would lose, capturing a single-stack template under that token; returns null for plain
    /// items (the caller then uses the ordinary id path). Idempotent: identical flavors share a token
    /// and one template, so they consolidate in the deposit plan.
    /// </summary>
    public string? Register(Item? item)
    {
        if (item is not SObject obj)
            return null;

        var preserveId = obj.preservedParentSheetIndex.Value;
        var preserveType = obj.preserve.Value;
        var colored = obj as ColoredObject;
        if (string.IsNullOrEmpty(preserveId) && preserveType is null && colored is null)
            return null;   // plain item — nothing the id can't reconstruct

        var packedColor = colored is not null ? colored.color.Value.PackedValue : 0u;
        var token = $"{obj.QualifiedItemId}|{preserveId}|{(preserveType?.ToString() ?? string.Empty)}|{packedColor}";

        if (!_templates.ContainsKey(token))
        {
            // getOne() copies preserve/PreserveId (and ColoredObject copies color + price) — a faithful
            // one-stack template. Quality is applied per-stack at rebuild, so normalize it off here.
            if (obj.getOne() is SObject template)
            {
                template.Stack = 1;
                template.Quality = 0;
                _templates[token] = template;
            }
            else
            {
                return null;
            }
        }

        return token;
    }

    /// <summary>
    /// Clone the captured template for <paramref name="flavorId"/> at the given stack and quality, or
    /// null when the token is unknown (caller falls back to the id-based rebuild).
    /// </summary>
    public static Item? Rebuild(IReadOnlyDictionary<string, SObject> templates, string flavorId, int quantity, int quality)
    {
        if (string.IsNullOrEmpty(flavorId) || !templates.TryGetValue(flavorId, out var template))
            return null;

        if (template.getOne() is not SObject clone)
            return null;

        clone.Stack = Math.Max(1, quantity);
        if (quality > 0)
            clone.Quality = quality;
        return clone;
    }
}
