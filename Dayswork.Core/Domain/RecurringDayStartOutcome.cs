namespace Dayswork.Core.Domain;

public sealed record RecurringDayStartOutcome(
    RecurringTermsRefreshOutcome Refresh,
    int DailyPrice,
    int Shortfall,
    bool ShouldPersistTermsSnapshot,
    bool ShouldChargePlayer,
    bool ShouldStartShift,
    RecurringDayStartNoticeKind NoticeKind);
