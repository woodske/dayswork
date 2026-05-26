using Dayswork.Core.Inventory;
using FsCheck;
using FsCheck.Xunit;

namespace Dayswork.Tests.U22;

public sealed class OverflowCategorizerPropertyTests
{
    [Property(MaxTest = 500)]
    public Property Categorize_Is_Stable_Across_Permutations()
    {
        return Prop.ForAll(U22PropertyGenerators.OverflowItems(), overflow =>
        {
            var categorizer = new OverflowCategorizer();
            var expected = categorizer.Categorize(overflow);
            var reversed = categorizer.Categorize(overflow.Reverse().ToList());

            return expected.SequenceEqual(reversed);
        });
    }
}
