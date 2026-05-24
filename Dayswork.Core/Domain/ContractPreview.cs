namespace Dayswork.Core.Domain;

public sealed record ContractPreview(
    bool IsValid,
    IReadOnlyList<ContractValidationIssue> ValidationIssues,
    ContractTermsSnapshot? ProposedTerms);
