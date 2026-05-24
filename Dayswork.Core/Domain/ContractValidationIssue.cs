namespace Dayswork.Core.Domain;

public sealed record ContractValidationIssue(ContractValidationCode Code, TaskKind? RelatedTask);
