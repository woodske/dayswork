namespace Dayswork.Tests.Generators;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using FsCheck;

public static class PricingGen
{
    private static readonly TaskKind[] AllTasks = Enum.GetValues<TaskKind>();

    // Random subset of TaskKind values (may be empty).
    public static Arbitrary<IReadOnlyList<TaskKind>> TaskSubset() =>
        (from bits in Gen.Choose(0, (1 << AllTasks.Length) - 1)
         select (IReadOnlyList<TaskKind>)AllTasks
             .Where((_, i) => (bits & (1 << i)) != 0)
             .ToList()).ToArbitrary();

    // Valid hourly rate relative to a config snapshot.
    public static Arbitrary<int> ValidRate(IConfigSnapshot config) =>
        Gen.Choose(config.BaseRate,
                   config.BaseRate + config.TaskIncrements.Values.Sum())
           .ToArbitrary();

    // Positive estimated hours in a practical range.
    public static Arbitrary<double> ValidEstimatedHours() =>
        (from hundredths in Gen.Choose(1, 2000)
         select hundredths / 100.0).ToArbitrary();

    // Deposit amounts in a practical gold range.
    public static Arbitrary<int> ValidDeposit() =>
        Gen.Choose(1, 100_000).ToArbitrary();

    // Hours worked in [0.0, maxHours] — used for RefundCalculator PBTs.
    public static Arbitrary<double> ValidHoursWorked(double maxHours) =>
        (from hundredths in Gen.Choose(0, (int)(maxHours * 100))
         select hundredths / 100.0).ToArbitrary();
}
