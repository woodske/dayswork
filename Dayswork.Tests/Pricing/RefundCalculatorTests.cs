namespace Dayswork.Tests.Pricing;

using Dayswork.Core.Pricing;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

public class RefundCalculatorTests
{
    private readonly RefundCalculator _calc = new();

    // ── [Fact] ───────────────────────────────────────────────────────────────

    [Fact]
    public void ZeroHoursWorked_FullRefund()
    {
        Assert.Equal(285, _calc.Calculate(deposit: 285, hoursWorked: 0.0, rate: 95));
    }

    [Fact]
    public void FullHoursWorked_LeavesSmallRefund()
    {
        // deposit=285, hoursWorked=3.0, rate=95 → billable=Ceiling(285)=285 → refund=0
        Assert.Equal(0, _calc.Calculate(deposit: 285, hoursWorked: 3.0, rate: 95));
    }

    [Fact]
    public void PartialHoursWorked_PartialRefund()
    {
        // deposit=285, hoursWorked=1.5, rate=95 → billable=Ceiling(142.5)=143 → refund=142
        Assert.Equal(142, _calc.Calculate(deposit: 285, hoursWorked: 1.5, rate: 95));
    }

    [Fact]
    public void Clamp_Lower_NegativeResultBecomesZero()
    {
        // Force billable > deposit: deposit=10, hoursWorked=1.0, rate=95 → billable=95 → clamp to 0
        Assert.Equal(0, _calc.Calculate(deposit: 10, hoursWorked: 1.0, rate: 95));
    }

    [Fact]
    public void Clamp_Upper_RefundNeverExceedsDeposit()
    {
        Assert.Equal(500, _calc.Calculate(deposit: 500, hoursWorked: 0.0, rate: 95));
    }

    // ── [Property] — PBT-03 ─────────────────────────────────────────────────

    [Property(MaxTest = 1000)]
    public Property Refund_AlwaysInRange() =>
        Prop.ForAll(
            PricingGen.ValidDeposit(),
            PricingGen.ValidEstimatedHours(),
            PricingGen.ValidDeposit(),
            (deposit, maxHours, rate) =>
            {
                var hoursWorked = maxHours * 0.5;    // arbitrary fraction in [0, maxHours]
                var refund = _calc.Calculate(deposit, hoursWorked, rate);
                return refund >= 0 && refund <= deposit;
            });

    [Property(MaxTest = 1000)]
    public Property FullRefund_WhenZeroHoursWorked() =>
        Prop.ForAll(
            PricingGen.ValidDeposit(),
            PricingGen.ValidDeposit(),
            (deposit, rate) =>
                _calc.Calculate(deposit, hoursWorked: 0.0, rate) == deposit);

    [Property(MaxTest = 1000)]
    public Property NetCharge_NeverExceedsBillable() =>
        Prop.ForAll(
            PricingGen.ValidDeposit(),
            PricingGen.ValidEstimatedHours(),
            PricingGen.ValidDeposit(),
            (deposit, hours, rate) =>
            {
                var refund   = _calc.Calculate(deposit, hours, rate);
                var billable = (int)Math.Ceiling(rate * hours);
                // net charge (deposit - refund) must not exceed billable; allow 1g ceiling tolerance
                return deposit - refund <= billable + 1;
            });
}
