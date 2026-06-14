namespace Dayswork.Core.Compat;

/// <summary>
/// Deterministically selects the active expansion profile from the set of installed mod ids
///. Profiles are evaluated in priority order; the first whose
/// <see cref="IExpansionProfile.Matches"/> returns true wins. The Vanilla profile must be supplied
/// last as the always-matching fallback, so selection is total and order-independent of input.
/// </summary>
public sealed class ExpansionProfileSelector
{
    private readonly IReadOnlyList<IExpansionProfile> _orderedProfiles;

    /// <param name="orderedProfiles">
    /// Profiles in priority order, expansion profiles first and the Vanilla fallback last.
    /// </param>
    public ExpansionProfileSelector(IReadOnlyList<IExpansionProfile> orderedProfiles)
    {
        if (orderedProfiles is null || orderedProfiles.Count == 0)
            throw new ArgumentException("At least one profile (the Vanilla fallback) is required.", nameof(orderedProfiles));

        _orderedProfiles = orderedProfiles;
    }

    /// <summary>Returns the highest-priority profile that matches the installed mod ids.</summary>
    public IExpansionProfile Select(IReadOnlySet<string> installedModIds)
    {
        foreach (var profile in _orderedProfiles)
        {
            if (profile.Matches(installedModIds))
                return profile;
        }

        // Unreachable when an always-matching Vanilla profile is supplied last, but stay total.
        return _orderedProfiles[^1];
    }
}
