using Dayswork.Core.Domain;

namespace Dayswork.Core.Capabilities;

/// <summary>
/// Evaluates whether a worker's tool snapshot is capable of processing a given object.
/// </summary>
public interface ICapabilityEvaluator
{
    /// <summary>Returns true if the worker can chop the target with their current axe level.</summary>
    bool CanChop(ToolSnapshot snap, AxeTarget target);

    /// <summary>Returns true if the worker can break the target with their current pickaxe level.</summary>
    bool CanBreak(ToolSnapshot snap, PickTarget target);
}
