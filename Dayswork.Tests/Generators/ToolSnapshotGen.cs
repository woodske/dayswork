using Dayswork.Core.Domain;
using FsCheck;

namespace Dayswork.Tests.Generators;

/// <summary>
/// FsCheck generator for ToolSnapshot. Provides uniform distribution over all
/// five ToolLevel values. Available to downstream units (U-10+) via PBT-07 obligation.
/// </summary>
public static class ToolSnapshotGen
{
    private static readonly Arbitrary<ToolLevel> ArbToolLevel =
        Arb.From(Gen.Elements(
            ToolLevel.Basic,
            ToolLevel.Copper,
            ToolLevel.Steel,
            ToolLevel.Gold,
            ToolLevel.Iridium));

    public static readonly Arbitrary<ToolSnapshot> ArbToolSnapshot =
        Arb.From(
            from axe  in ArbToolLevel.Generator
            from pick in ArbToolLevel.Generator
            from can  in ArbToolLevel.Generator
            select new ToolSnapshot(axe, pick, can));
}
