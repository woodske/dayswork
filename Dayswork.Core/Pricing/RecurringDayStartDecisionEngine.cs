namespace Dayswork.Core.Pricing;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;

public sealed class RecurringDayStartDecisionEngine
{
    private readonly IContractTermsBuilder _termsBuilder;

    public RecurringDayStartDecisionEngine(IContractTermsBuilder termsBuilder)
    {
        _termsBuilder = termsBuilder;
    }

    public RecurringDayStartOutcome Evaluate(
        Contract contract,
        IConfigSnapshot config,
        bool festivalToday,
        int availableGold)
    {
        if (contract.ScopeSelection is null)
        {
            return new RecurringDayStartOutcome(
                Refresh: new RecurringTermsRefreshOutcome(
                    RecurringTermsRefreshStatus.Unsupported,
                    TermsSnapshot: null),
                DailyPrice: 0,
                Shortfall: 0,
                ShouldPersistTermsSnapshot: false,
                ShouldChargePlayer: false,
                ShouldStartShift: false,
                NoticeKind: RecurringDayStartNoticeKind.NeedsAttention);
        }

        var preview = _termsBuilder.BuildPreview(contract.ScopeSelection, contract.EnabledTasks, config);
        if (!preview.IsValid || preview.ProposedTerms is null)
        {
            return new RecurringDayStartOutcome(
                Refresh: new RecurringTermsRefreshOutcome(
                    RecurringTermsRefreshStatus.InvalidNeedsAttention,
                    TermsSnapshot: null),
                DailyPrice: 0,
                Shortfall: 0,
                ShouldPersistTermsSnapshot: false,
                ShouldChargePlayer: false,
                ShouldStartShift: false,
                NoticeKind: RecurringDayStartNoticeKind.NeedsAttention);
        }

        var dailyPrice = preview.ProposedTerms.Pricing.TotalPrice;
        if (festivalToday)
        {
            return new RecurringDayStartOutcome(
                Refresh: new RecurringTermsRefreshOutcome(
                    RecurringTermsRefreshStatus.Valid,
                    preview.ProposedTerms),
                DailyPrice: dailyPrice,
                Shortfall: 0,
                ShouldPersistTermsSnapshot: true,
                ShouldChargePlayer: false,
                ShouldStartShift: false,
                NoticeKind: RecurringDayStartNoticeKind.FestivalSkip);
        }

        if (availableGold < dailyPrice)
        {
            return new RecurringDayStartOutcome(
                Refresh: new RecurringTermsRefreshOutcome(
                    RecurringTermsRefreshStatus.Valid,
                    preview.ProposedTerms),
                DailyPrice: dailyPrice,
                Shortfall: dailyPrice - availableGold,
                ShouldPersistTermsSnapshot: true,
                ShouldChargePlayer: false,
                ShouldStartShift: false,
                NoticeKind: RecurringDayStartNoticeKind.CannotAfford);
        }

        return new RecurringDayStartOutcome(
            Refresh: new RecurringTermsRefreshOutcome(
                RecurringTermsRefreshStatus.Valid,
                preview.ProposedTerms),
            DailyPrice: dailyPrice,
            Shortfall: 0,
            ShouldPersistTermsSnapshot: true,
            ShouldChargePlayer: true,
            ShouldStartShift: true,
            NoticeKind: RecurringDayStartNoticeKind.None);
    }
}
