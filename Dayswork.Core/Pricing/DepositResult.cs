namespace Dayswork.Core.Pricing;

public abstract record DepositResult;

public sealed record PositiveDeposit(int Amount) : DepositResult;

public sealed record ZeroDeposit : DepositResult
{
    public static readonly ZeroDeposit Instance = new();
}
