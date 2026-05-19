namespace Dayswork.Core.Pricing;

public sealed class RefundCalculator : IRefundCalculator
{
    public int Calculate(int deposit, double hoursWorked, int rate)
    {
        var billable = (int)Math.Ceiling(rate * hoursWorked);
        return Math.Clamp(deposit - billable, 0, deposit);
    }
}
