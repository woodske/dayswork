using Dayswork.Core.Compat;
using FsCheck;

namespace Dayswork.Tests.Compat;

/// <summary>FsCheck generators for expansion-compat property tests.</summary>
public static class ExpansionCompatGenerators
{
    private static readonly string[] OtherModIds =
    {
        "Pathoschild.ContentPatcher",
        "Esca.FarmTypeManager",
        "DIGUS.MailFrameworkMod",
        "spacechase0.GenericModConfigMenu",
        "Some.Other.Mod",
    };

    /// <summary>
    /// Generates arbitrary installed-mod-id sets that may or may not include the SVE content/code
    /// ids, plus a random selection of unrelated mod ids.
    /// </summary>
    public static Arbitrary<IReadOnlySet<string>> InstalledModIdSets()
    {
        var gen =
            from includeSveContent in Arb.Generate<bool>()
            from includeSveCode in Arb.Generate<bool>()
            from extraCount in Gen.Choose(0, OtherModIds.Length)
            from extras in Gen.ArrayOf(extraCount, Gen.Elements(OtherModIds))
            select BuildSet(includeSveContent, includeSveCode, extras);

        return gen.ToArbitrary();
    }

    private static IReadOnlySet<string> BuildSet(bool includeSveContent, bool includeSveCode, string[] extras)
    {
        var set = new HashSet<string>(extras);
        if (includeSveContent)
            set.Add(SveExpansionProfile.ContentModId);
        if (includeSveCode)
            set.Add(SveExpansionProfile.CodeModId);
        return set;
    }
}
