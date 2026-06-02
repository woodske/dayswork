namespace Dayswork.Core.Domain;

/// <summary>
/// Immutable snapshot of the player's relevant tool levels, captured once at 6am spawn.
/// Locked for the duration of the shift — mid-day tool changes do not affect the worker (FR-TOOL-01).
/// If a tool was not owned, the Mod layer defaults its level to Basic and separately queues a warning.
/// </summary>
public sealed record ToolSnapshot(
    ToolLevel AxeLevel,
    ToolLevel PickaxeLevel,
    ToolLevel WateringCanLevel);
