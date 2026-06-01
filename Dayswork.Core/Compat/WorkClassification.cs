namespace Dayswork.Core.Compat;

/// <summary>
/// The kind of work an expansion content-classification override resolves to.
/// Minimal in U-SVE-01; the catalogue is refined when content overrides are
/// actually consumed in U-SVE-04.
/// </summary>
public enum WorkClassificationKind
{
    /// <summary>No override — the caller falls through to existing vanilla classification.</summary>
    None,

    /// <summary>Axe-relevant work.</summary>
    Axe,

    /// <summary>Pickaxe-relevant work.</summary>
    Pick,
}

/// <summary>
/// Result of an expansion content-classification override. Pure value type.
/// </summary>
public readonly record struct WorkClassification(WorkClassificationKind Kind)
{
    /// <summary>Convenience value meaning "no override applies".</summary>
    public static WorkClassification NoOverride => new(WorkClassificationKind.None);
}
