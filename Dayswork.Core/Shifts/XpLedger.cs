namespace Dayswork.Core.Shifts;

/// <summary>One skill's worth of experience owed to the sponsor.</summary>
/// <param name="Skill">Vanilla skill index: 0 farming, 1 fishing, 2 foraging, 3 mining, 4 combat.</param>
public readonly record struct SkillXpGrant(int Skill, int Amount);

/// <summary>
/// Experience the worker earned this shift, accumulated per skill and granted to the sponsor in
/// batches rather than per beat — so a shift produces one level-up notice per batch instead of one
/// per swing. Amounts are harvested as a difference around each guarded worker beat (vanilla never
/// asks whose XP it is; it credits whichever farmer it has in hand), so this only has to add up
/// and hand back what it has.
/// </summary>
public sealed class XpLedger
{
    /// <summary>Skills 0-4. Luck (5) is excluded: vanilla <c>gainExperience</c> ignores it.</summary>
    public const int SkillCount = 5;

    private readonly int[] _pending = new int[SkillCount];

    public void Add(int skill, int amount)
    {
        if (amount <= 0 || skill < 0 || skill >= SkillCount)
            return;

        _pending[skill] += amount;
    }

    /// <summary>Adds a per-skill delta array (a vanilla <c>experiencePoints</c>-shaped array of 5
    /// or 6 entries; anything past skill 4 is ignored).</summary>
    public void Add(IReadOnlyList<int> deltasBySkill)
    {
        var count = Math.Min(deltasBySkill.Count, SkillCount);
        for (var skill = 0; skill < count; skill++)
            Add(skill, deltasBySkill[skill]);
    }

    public int Pending(int skill) =>
        skill >= 0 && skill < SkillCount ? _pending[skill] : 0;

    public int TotalPending
    {
        get
        {
            var total = 0;
            foreach (var amount in _pending)
                total += amount;
            return total;
        }
    }

    /// <summary>Takes everything owed and clears the ledger. Returns only the skills with a
    /// non-zero amount, lowest skill index first.</summary>
    public IReadOnlyList<SkillXpGrant> Flush()
    {
        var grants = new List<SkillXpGrant>(SkillCount);
        for (var skill = 0; skill < SkillCount; skill++)
        {
            if (_pending[skill] <= 0)
                continue;

            grants.Add(new SkillXpGrant(skill, _pending[skill]));
            _pending[skill] = 0;
        }

        return grants;
    }
}
