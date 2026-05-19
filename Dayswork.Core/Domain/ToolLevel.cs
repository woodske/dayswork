namespace Dayswork.Core.Domain;

/// <summary>
/// Upgrade level of a player tool, read once at 6am spawn and locked for the shift.
/// Integer values match Stardew Valley's internal UpgradeLevel int directly.
/// </summary>
public enum ToolLevel
{
    Basic    = 0,
    Copper   = 1,
    Steel    = 2,
    Gold     = 3,
    Iridium  = 4,
}
