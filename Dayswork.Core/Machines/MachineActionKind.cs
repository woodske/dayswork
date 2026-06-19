namespace Dayswork.Core.Machines;

/// <summary>The two things a machine visit can do, in order: collect ready output, then reload if empty.</summary>
public enum MachineActionKind
{
    Collect,
    Load,
}
