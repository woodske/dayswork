namespace Dayswork.Core.Pricing;

public interface IDepositCalculator
{
    DepositResult Calculate(double estimatedHours, int rate);
}
