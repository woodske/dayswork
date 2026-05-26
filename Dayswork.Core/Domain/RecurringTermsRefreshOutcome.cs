namespace Dayswork.Core.Domain;

public sealed record RecurringTermsRefreshOutcome(
    RecurringTermsRefreshStatus Status,
    ContractTermsSnapshot? TermsSnapshot);
