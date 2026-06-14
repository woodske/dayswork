using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Tests.Persistence;
using Dayswork.Tests.Pricing;
using FsCheck;
using FsCheck.Xunit;

namespace Dayswork.Tests.Scheduling;

public sealed class RecurringDayStartDecisionPropertyTests
{
    private readonly RecurringDayStartDecisionEngine _engine = new(ContractTermsBuilderFactory.CreateTermsBuilder());

    [Property(Arbitrary = new[] { typeof(RecurringDecisionGenerators) }, MaxTest = 300)]
    public bool Decision_outcomes_are_deterministic(U23RecurringDecisionCase input)
    {
        var first = _engine.Evaluate(input.Contract, input.Config, input.FestivalToday, input.AvailableGold);
        var second = _engine.Evaluate(input.Contract, input.Config, input.FestivalToday, input.AvailableGold);
        return OutcomesEqual(first, second);
    }

    [Property(Arbitrary = new[] { typeof(RecurringDecisionGenerators) }, MaxTest = 300)]
    public bool Festival_days_never_charge_or_start(U23RecurringContractCase input)
    {
        var outcome = _engine.Evaluate(input.Contract, input.Config, festivalToday: true, availableGold: int.MaxValue);
        return outcome.Refresh.Status == RecurringTermsRefreshStatus.Valid
            && outcome.ShouldPersistTermsSnapshot
            && !outcome.ShouldChargePlayer
            && !outcome.ShouldStartShift
            && outcome.NoticeKind == RecurringDayStartNoticeKind.FestivalSkip;
    }

    [Property(Arbitrary = new[] { typeof(RecurringDecisionGenerators) }, MaxTest = 300)]
    public bool Exact_affordability_is_enough_but_one_short_is_not(U23RecurringContractCase input)
    {
        var baseline = _engine.Evaluate(input.Contract, input.Config, festivalToday: false, availableGold: int.MaxValue);
        var requiredPrice = baseline.DailyPrice;
        var exact = _engine.Evaluate(input.Contract, input.Config, festivalToday: false, availableGold: requiredPrice);
        var oneShort = requiredPrice == 0
            ? exact
            : _engine.Evaluate(input.Contract, input.Config, festivalToday: false, availableGold: requiredPrice - 1);

        return exact.ShouldChargePlayer
            && exact.ShouldStartShift
            && exact.NoticeKind == RecurringDayStartNoticeKind.None
            && (requiredPrice == 0
                || (!oneShort.ShouldChargePlayer
                    && !oneShort.ShouldStartShift
                    && oneShort.ShouldPersistTermsSnapshot
                    && oneShort.NoticeKind == RecurringDayStartNoticeKind.CannotAfford
                    && oneShort.Shortfall == 1));
    }

    [Property(Arbitrary = new[] { typeof(RecurringDecisionGenerators) }, MaxTest = 300)]
    public Property Successful_refreshes_only_mutate_terms_snapshot(U23RecurringDecisionCase input)
    {
        var store = new ContractStore(_ => { });
        store.Add(input.Contract);

        var outcome = _engine.Evaluate(input.Contract, input.Config, input.FestivalToday, input.AvailableGold);
        if (!outcome.ShouldPersistTermsSnapshot || outcome.Refresh.TermsSnapshot is null)
            return true.ToProperty();

        store.ReplaceTermsSnapshot(input.Contract.Id, outcome.Refresh.TermsSnapshot);
        var stored = store.Get(input.Contract.Id);

        return ContractStructuralComparer.ContractsEqual(
                input.Contract with { TermsSnapshot = outcome.Refresh.TermsSnapshot },
                stored)
            .ToProperty()
            .Label($"price={outcome.DailyPrice} notice={outcome.NoticeKind}");
    }

    private static bool OutcomesEqual(RecurringDayStartOutcome left, RecurringDayStartOutcome right) =>
        left.Refresh.Status == right.Refresh.Status
        && ContractStructuralComparer.TermsSnapshotsEqual(left.Refresh.TermsSnapshot, right.Refresh.TermsSnapshot)
        && left.DailyPrice == right.DailyPrice
        && left.Shortfall == right.Shortfall
        && left.ShouldPersistTermsSnapshot == right.ShouldPersistTermsSnapshot
        && left.ShouldChargePlayer == right.ShouldChargePlayer
        && left.ShouldStartShift == right.ShouldStartShift
        && left.NoticeKind == right.NoticeKind;
}
