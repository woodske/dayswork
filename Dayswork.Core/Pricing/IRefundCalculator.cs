namespace Dayswork.Core.Pricing;

public interface IRefundCalculator
{
    int Calculate(int deposit, double hoursWorked, int rate);
}
