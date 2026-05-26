using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Persistence;
using Dayswork.Tests.Persistence.Generators;
using Dayswork.Tests.Pricing;
using Xunit;

namespace Dayswork.Tests.U23;

public sealed class RecurringDayStartDecisionEngineTests
{
    private readonly RecurringDayStartDecisionEngine _engine = new(U18BuilderFactory.CreateTermsBuilder());

    [Fact]
    public void FestivalDay_ValidRecurringContract_RefreshesTermsWithoutChargeOrSpawn()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract();
        var config = ConfigDefaults.Build();

        var outcome = _engine.Evaluate(contract, config, festivalToday: true, availableGold: int.MaxValue);

        Assert.Equal(RecurringTermsRefreshStatus.Valid, outcome.Refresh.Status);
        Assert.NotNull(outcome.Refresh.TermsSnapshot);
        Assert.True(outcome.ShouldPersistTermsSnapshot);
        Assert.False(outcome.ShouldChargePlayer);
        Assert.False(outcome.ShouldStartShift);
        Assert.Equal(RecurringDayStartNoticeKind.FestivalSkip, outcome.NoticeKind);
        Assert.Equal(outcome.Refresh.TermsSnapshot!.Pricing.TotalPrice, outcome.DailyPrice);
    }

    [Fact]
    public void ExactAffordability_StartsShiftAndChargesPrice()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract();
        var config = ConfigDefaults.Build();
        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(contract.ScopeSelection!, contract.EnabledTasks, config);
        var exactGold = preview.ProposedTerms!.Pricing.TotalPrice;

        var outcome = _engine.Evaluate(contract, config, festivalToday: false, availableGold: exactGold);

        Assert.Equal(RecurringTermsRefreshStatus.Valid, outcome.Refresh.Status);
        Assert.True(outcome.ShouldPersistTermsSnapshot);
        Assert.True(outcome.ShouldChargePlayer);
        Assert.True(outcome.ShouldStartShift);
        Assert.Equal(RecurringDayStartNoticeKind.None, outcome.NoticeKind);
        Assert.Equal(0, outcome.Shortfall);
        Assert.Equal(exactGold, outcome.DailyPrice);
    }

    [Fact]
    public void MissingTypedScope_ReturnsNeedsAttentionWithoutPersisting()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract() with { ScopeSelection = null };
        var config = ConfigDefaults.Build();

        var outcome = _engine.Evaluate(contract, config, festivalToday: false, availableGold: int.MaxValue);

        Assert.Equal(RecurringTermsRefreshStatus.Unsupported, outcome.Refresh.Status);
        Assert.Null(outcome.Refresh.TermsSnapshot);
        Assert.False(outcome.ShouldPersistTermsSnapshot);
        Assert.False(outcome.ShouldChargePlayer);
        Assert.False(outcome.ShouldStartShift);
        Assert.Equal(RecurringDayStartNoticeKind.NeedsAttention, outcome.NoticeKind);
    }

    [Fact]
    public void InvalidRefresh_DoesNotReplacePreviouslySavedTerms()
    {
        var store = new ContractStore(_ => { });
        var original = U19PersistenceGen.CreateExampleCurrentSchemaContract();
        store.Add(original);

        var invalidContract = original with { ScopeSelection = null };
        var outcome = _engine.Evaluate(invalidContract, ConfigDefaults.Build(), festivalToday: false, availableGold: int.MaxValue);
        if (outcome.ShouldPersistTermsSnapshot && outcome.Refresh.TermsSnapshot is not null)
            store.ReplaceTermsSnapshot(invalidContract.Id, outcome.Refresh.TermsSnapshot);

        Assert.True(ContractStructuralComparer.ContractsEqual(original, store.Get(original.Id)));
    }

    [Fact]
    public void UnaffordableRefresh_SkipsWorkButKeepsRefreshedTermsEligibleForPersistence()
    {
        var contract = U19PersistenceGen.CreateExampleCurrentSchemaContract();
        var config = ConfigDefaults.Build();
        var preview = U18BuilderFactory.CreateTermsBuilder().BuildPreview(contract.ScopeSelection!, contract.EnabledTasks, config);
        var dailyPrice = preview.ProposedTerms!.Pricing.TotalPrice;

        var outcome = _engine.Evaluate(contract, config, festivalToday: false, availableGold: Math.Max(0, dailyPrice - 1));

        Assert.Equal(RecurringTermsRefreshStatus.Valid, outcome.Refresh.Status);
        Assert.NotNull(outcome.Refresh.TermsSnapshot);
        Assert.True(outcome.ShouldPersistTermsSnapshot);
        Assert.False(outcome.ShouldChargePlayer);
        Assert.False(outcome.ShouldStartShift);
        Assert.Equal(RecurringDayStartNoticeKind.CannotAfford, outcome.NoticeKind);
        Assert.Equal(dailyPrice, outcome.DailyPrice);
        Assert.Equal(dailyPrice == 0 ? 0 : 1, outcome.Shortfall);
    }
}
