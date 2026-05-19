namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Pricing;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

public class DepositCalculatorTests
{
    private readonly DepositCalculator _calc = new();

    // ── [Fact] ───────────────────────────────────────────────────────────────

    [Fact]
    public void ZeroEstimatedHours_ReturnsZeroDeposit()
    {
        Assert.IsType<ZeroDeposit>(_calc.Calculate(0.0, 95));
    }

    [Fact]
    public void NegativeEstimatedHours_ReturnsZeroDeposit()
    {
        Assert.IsType<ZeroDeposit>(_calc.Calculate(-1.0, 95));
    }

    [Fact]
    public void PositiveHours_ReturnsPositiveDeposit()
    {
        Assert.IsType<PositiveDeposit>(_calc.Calculate(1.0, 95));
    }

    [Fact]
    public void ExactHours_NoCeilingEffect()
    {
        // 70 * 0.5 = 35.0 exactly → Ceiling = 35
        var result = Assert.IsType<PositiveDeposit>(_calc.Calculate(0.5, 70));
        Assert.Equal(35, result.Amount);
    }

    [Fact]
    public void CeilingApplied_RoundsUp()
    {
        // 70 * 0.5001 = 35.007 → Ceiling = 36
        var result = Assert.IsType<PositiveDeposit>(_calc.Calculate(0.5001, 70));
        Assert.Equal(36, result.Amount);
    }

    // ── [Property] — PBT-03 ─────────────────────────────────────────────────

    [Property(MaxTest = 1000)]
    public Property Amount_AlwaysNonNegative_WhenPositive() =>
        Prop.ForAll(
            PricingGen.ValidEstimatedHours(),
            PricingGen.ValidDeposit(),
            (hours, rate) =>
            {
                var result = _calc.Calculate(hours, rate);
                return result is not PositiveDeposit p || p.Amount >= 0;
            });

    [Property(MaxTest = 1000)]
    public Property Amount_AtLeastFloor_WhenPositive() =>
        Prop.ForAll(
            PricingGen.ValidEstimatedHours(),
            PricingGen.ValidDeposit(),
            (hours, rate) =>
            {
                var result = _calc.Calculate(hours, rate);
                if (result is not PositiveDeposit p) return true;
                return p.Amount >= (int)Math.Floor(rate * hours);
            });
}
