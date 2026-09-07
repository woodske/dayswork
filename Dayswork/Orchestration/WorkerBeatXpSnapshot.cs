using Netcode;
using StardewValley;

namespace Dayswork.Orchestration;

/// <summary>
/// The host player's experience state around one guarded worker beat.
/// <para>
/// Vanilla never asks whose XP an action earned — it credits whichever farmer it has in hand. Two
/// farmers end up holding worker XP after a beat: the throwaway action farmer (tree and rock XP,
/// credited to the tool's last user) and <c>Game1.player</c> itself (<c>Crop.harvest</c> hardcodes
/// it). This snapshot puts the host's share back where it was and reports what was taken, so the
/// mod can hand exactly that to the contract's sponsor instead — one code path whether the sponsor
/// is the host, a guest, or offline. See <c>docs/game-data/multiplayer-and-ownership.md</c>.
/// </para>
/// </summary>
internal readonly struct WorkerBeatXpSnapshot
{
    /// <summary>Vanilla's <c>experiencePoints</c> array length: skills 0-4 plus luck.</summary>
    private const int SkillArrayLength = 6;

    private static readonly Func<Farmer, NetInt>[] SkillLevelFields =
    {
        farmer => farmer.farmingLevel,
        farmer => farmer.fishingLevel,
        farmer => farmer.foragingLevel,
        farmer => farmer.miningLevel,
        farmer => farmer.combatLevel,
        farmer => farmer.luckLevel,
    };

    private readonly int[] _experience;
    private readonly int[] _levels;
    private readonly int _newLevelCount;
    private readonly uint _masteryExp;

    private WorkerBeatXpSnapshot(int[] experience, int[] levels, int newLevelCount, uint masteryExp)
    {
        _experience = experience;
        _levels = levels;
        _newLevelCount = newLevelCount;
        _masteryExp = masteryExp;
    }

    public static WorkerBeatXpSnapshot Capture(Farmer host)
    {
        var experience = new int[SkillArrayLength];
        var levels = new int[SkillArrayLength];
        for (var skill = 0; skill < SkillArrayLength; skill++)
        {
            experience[skill] = host.experiencePoints[skill];
            levels[skill] = SkillLevelFields[skill](host).Value;
        }

        return new WorkerBeatXpSnapshot(
            experience,
            levels,
            host.newLevels.Count,
            Game1.stats.Get("MasteryExp"));
    }

    /// <summary>
    /// Puts the host's experience, skill levels, pending level-ups and mastery progress back as
    /// they were, and returns what the beat added per skill. (The level-up textbox the beat may
    /// have queued is removed by the guard's own HUD trim.)
    /// </summary>
    public int[] RestoreAndDiff(Farmer host)
    {
        var earned = new int[SkillArrayLength];
        for (var skill = 0; skill < SkillArrayLength; skill++)
        {
            earned[skill] = Math.Max(0, host.experiencePoints[skill] - _experience[skill]);
            host.experiencePoints[skill] = _experience[skill];

            var level = SkillLevelFields[skill](host);
            if (level.Value != _levels[skill])
                level.Value = _levels[skill];
        }

        while (host.newLevels.Count > _newLevelCount)
            host.newLevels.RemoveAt(host.newLevels.Count - 1);

        if (Game1.stats.Get("MasteryExp") != _masteryExp)
            Game1.stats.Set("MasteryExp", _masteryExp);

        return earned;
    }
}
