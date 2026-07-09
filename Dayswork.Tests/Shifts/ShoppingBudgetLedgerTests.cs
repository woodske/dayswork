using Dayswork.Core.Domain;
using Dayswork.Core.Shifts;
using Xunit;

namespace Dayswork.Tests.Shifts;

public sealed class ShoppingBudgetLedgerTests
{
    private static readonly ContractId OwnerA = ContractId.New();
    private static readonly ContractId OwnerB = ContractId.New();
    private static readonly ContractId OwnerC = ContractId.New();

    [Fact]
    public void ReservedByOthers_EmptyLedger_IsZero()
    {
        var ledger = new ShoppingBudgetLedger();

        Assert.Equal(0, ledger.ReservedByOthers(OwnerA));
    }

    [Fact]
    public void ReservedByOthers_ExcludesTheOwnersOwnReservation()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);

        Assert.Equal(0, ledger.ReservedByOthers(OwnerA));
    }

    [Fact]
    public void ReservedByOthers_SumsEveryOtherContract()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);
        ledger.Reserve(OwnerB, 120);
        ledger.Reserve(OwnerC, 30);

        Assert.Equal(150, ledger.ReservedByOthers(OwnerA));
        Assert.Equal(530, ledger.ReservedByOthers(OwnerB));
    }

    [Fact]
    public void Reserve_OverwritesAPriorReservationForTheSameOwner()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);
        ledger.Reserve(OwnerA, 200);

        Assert.Equal(200, ledger.ReservedByOthers(OwnerB));
    }

    [Fact]
    public void Release_ClearsTheReservation()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);
        ledger.Release(OwnerA);

        Assert.Equal(0, ledger.ReservedByOthers(OwnerB));
    }

    [Fact]
    public void Reserve_NonPositiveAmount_ClearsTheReservation()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);
        ledger.Reserve(OwnerA, 0);

        Assert.Equal(0, ledger.ReservedByOthers(OwnerB));
    }

    [Fact]
    public void Release_UnknownOwner_IsHarmless()
    {
        var ledger = new ShoppingBudgetLedger();
        ledger.Reserve(OwnerA, 500);
        ledger.Release(OwnerB);

        Assert.Equal(500, ledger.ReservedByOthers(OwnerB));
    }
}
