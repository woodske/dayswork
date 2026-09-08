namespace Dayswork.Core.Domain;

/// <summary>
/// The recolour applied to one masked region of the worker's sprite sheet.
///
/// The numbers are exactly the three values the game's own building painter takes
/// (<c>BuildingPaintColor.Color*Hue/Saturation/Lightness</c>). That painter first desaturates the
/// masked pixels to grey and then applies these as shifts, so in practice they read as absolutes:
/// <paramref name="Hue"/> is the resulting hue in degrees (0–359), <paramref name="Saturation"/> is
/// the resulting saturation as a percentage (0–100), and <paramref name="Lightness"/> is a
/// percentage-point shift (−100…100) applied to the pixel's original lightness — which is what
/// preserves the sprite's shading ramp.
/// </summary>
public readonly record struct AppearanceTint(int Hue, int Saturation, int Lightness);

/// <summary>
/// One curated worker colour palette. The three regions correspond to the red / lime / blue areas
/// of <c>assets/farmhand_PaintMask.png</c>; a null region keeps the base sheet's own colours.
/// </summary>
public sealed record WorkerAppearance(
    string Key,
    AppearanceTint? Cap,
    AppearanceTint? Shirt,
    AppearanceTint? Overalls)
{
    /// <summary>Whether this palette recolours anything. False for the base sheet's own palette,
    /// which is served as the plain sprite asset rather than a painted variant.</summary>
    public bool HasTint => Cap is not null || Shirt is not null || Overalls is not null;
}

/// <summary>The curated palette list the Appearance preference cycles through.</summary>
public static class WorkerAppearances
{
    /// <summary>The palette an unset (or unrecognised) appearance key resolves to.</summary>
    public const string DefaultKey = "denim";

    public static readonly IReadOnlyList<WorkerAppearance> All = new[]
    {
        //                    key           cap                       shirt                    overalls
        new WorkerAppearance(DefaultKey,    null,                     null,                    null),
        new WorkerAppearance("forest",      new(120, 55, -5),         new(40, 20, 5),          new(145, 50, -10)),
        new WorkerAppearance("crimson",     new(0, 70, 0),            new(30, 15, 8),          new(355, 60, -8)),
        new WorkerAppearance("saffron",     new(35, 80, 5),           new(200, 25, 0),         new(30, 70, -5)),
        new WorkerAppearance("plum",        new(285, 55, 0),          new(45, 25, 5),          new(275, 50, -8)),
        new WorkerAppearance("teal",        new(185, 65, 0),          new(25, 20, 8),          new(190, 55, -8)),
        new WorkerAppearance("slate",       new(215, 25, -5),         new(210, 12, 5),         new(220, 20, -10)),
        new WorkerAppearance("rose",        new(335, 60, 5),          new(20, 20, 10),         new(330, 45, 0)),
        new WorkerAppearance("ochre",       new(45, 60, 0),           new(90, 25, 5),          new(40, 55, -10)),
        new WorkerAppearance("charcoal",    new(0, 0, -30),           new(0, 0, -5),           new(0, 0, -35)),
    };

    /// <summary>The palette for a stored appearance key. Unset, unknown, and retired keys all fall
    /// back to the default so a save can never leave a worker without a sprite.</summary>
    public static WorkerAppearance Resolve(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var appearance in All)
            {
                if (string.Equals(appearance.Key, key, StringComparison.OrdinalIgnoreCase))
                    return appearance;
            }
        }

        return All[0];
    }

    /// <summary>Position of a key in the cycle; 0 (the default palette) for anything unrecognised.</summary>
    public static int IndexOf(string? key)
    {
        var resolved = Resolve(key);
        for (var i = 0; i < All.Count; i++)
        {
            if (ReferenceEquals(All[i], resolved))
                return i;
        }

        return 0;
    }

    /// <summary>The next palette in the cycle, wrapping past the end.</summary>
    public static string NextKey(string? key) => All[(IndexOf(key) + 1) % All.Count].Key;

    /// <summary>The previous palette in the cycle, wrapping past the start.</summary>
    public static string PreviousKey(string? key) => All[(IndexOf(key) + All.Count - 1) % All.Count].Key;
}
