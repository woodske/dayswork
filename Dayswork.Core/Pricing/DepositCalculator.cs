namespace Dayswork.Core.Pricing;

public sealed class DepositCalculator : IDepositCalculator
{
    public DepositResult Calculate(double estimatedHours, int rate)
    {
        if (estimatedHours <= 0.0)
            return ZeroDeposit.Instance;

        var amount = (int)Math.Ceiling(rate * estimatedHours);
        return new PositiveDeposit(amount);
    }
}
