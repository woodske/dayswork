using Dayswork.Core.Domain;

namespace Dayswork.Core.Capabilities;

/// <inheritdoc/>
public sealed class CapabilityEvaluator : ICapabilityEvaluator
{
    /// <inheritdoc/>
    public bool CanChop(ToolSnapshot snap, AxeTarget target) =>
        CapabilityMatrix.CanChop(snap.AxeLevel, target);

    /// <inheritdoc/>
    public bool CanBreak(ToolSnapshot snap, PickTarget target) =>
        CapabilityMatrix.CanBreak(snap.PickaxeLevel, target);
}
