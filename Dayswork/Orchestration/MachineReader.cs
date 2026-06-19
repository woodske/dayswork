using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace Dayswork.Orchestration;

/// <summary>
/// One item a machine type accepts as a primary input, discovered by probing the live machine API.
/// <paramref name="Category"/>/<paramref name="CategoryName"/> drive the "Any &lt;category&gt;"
/// shortcuts in the input picker.
/// </summary>
internal sealed record AcceptedInput(string QualifiedId, string DisplayName, int Category, string CategoryName);

/// <summary>
/// A companion item a machine consumes on every craft regardless of the primary input (coal for a
/// furnace), read from <see cref="MachineData.AdditionalConsumedItems"/>. Surfaced in the input
/// picker as auto-selected/locked — the load engine already consumes these automatically.
/// </summary>
internal sealed record RequiredCompanion(string QualifiedId, string DisplayName, int Count);

/// <summary>The collect/reload-relevant state of a placed machine.</summary>
internal enum MachineReadyState
{
    /// <summary>Finished output is waiting to be collected.</summary>
    ReadyToCollect,

    /// <summary>Nothing held — idle and (possibly) loadable.</summary>
    Empty,

    /// <summary>Holding an in-progress output; still processing.</summary>
    Busy,
}

/// <summary>
/// Live-world → pure adapter for machines (<c>Data/Machines</c> objects). Enumerates and resolves
/// machines, classifies their ready-state, and — by probing the runtime machine API against the
/// input chest's contents — produces the pure <see cref="MachineLoadCandidate"/>s the
/// <see cref="MachineInputPlanner"/> consumes. Performs no mutation (every API call is probe-only).
///
/// Schema/API verified in <c>docs/machines.md</c>; per-entry recipe facts (fish-smoker coal,
/// dehydrator ×5, keg flavoring) are read live here rather than hard-coded.
/// </summary>
internal sealed class MachineReader
{
    // Accepted-input lists are derived by sweeping the whole object catalog (~900 probes), so cache
    // per machine type for the authoring session. Keyed by qualified machine id.
    private readonly Dictionary<string, IReadOnlyList<AcceptedInput>> _acceptedInputCache = new(StringComparer.Ordinal);

    public static MachineData? GetMachineData(SObject obj) => obj.GetMachineData();

    public static bool IsMachine(SObject obj) => obj.GetMachineData() is not null;

    /// <summary>The machine data for a type's qualified id (no placed instance needed). Null ⇒ not a machine.</summary>
    public static MachineData? GetMachineDataForType(string qualifiedId) =>
        DataLoader.Machines(Game1.content).GetValueOrDefault(qualifiedId);

    /// <summary>
    /// True when this machine type takes a primary input (some output rule fires on
    /// <c>ItemPlacedInMachine</c>) and is reloadable. Input-less producers (bee houses, tappers) and
    /// crystalarium/incubators return false → the group is collect-only and has no input picker.
    /// Cheap: inspects the rules directly, no catalog sweep.
    /// </summary>
    public static bool AcceptsInput(MachineData? data) =>
        data is not null
        && IsReloadable(data)
        && data.OutputRules is { } rules
        && rules.Any(rule => rule.Triggers is { } triggers
            && triggers.Any(trigger => trigger.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)));

    /// <summary>
    /// Every catalog object the machine accepts as a primary input, discovered by probing the live
    /// machine API (so both <c>RequiredItemId</c> and <c>RequiredTags</c> rules resolve to concrete
    /// items without reimplementing context-tag matching). Cached per machine type. Probe-only.
    /// </summary>
    public IReadOnlyList<AcceptedInput> EnumerateAcceptedInputs(SObject machine, MachineData data, Farmer who, GameLocation location)
    {
        if (_acceptedInputCache.TryGetValue(machine.QualifiedItemId, out var cached))
            return cached;

        var results = new List<AcceptedInput>();
        if (AcceptsInput(data))
        {
            foreach (var key in Game1.objectData.Keys)
            {
                var probe = ItemRegistry.Create("(O)" + key, 999, allowNull: true);
                if (probe is null)
                    continue;

                if (!MachineDataUtility.TryGetMachineOutputRule(
                        machine, data, MachineOutputTrigger.ItemPlacedInMachine, probe, who, location,
                        out var rule, out var triggerRule, out _, out _)
                    || rule is null || triggerRule is null)
                {
                    continue;
                }

                results.Add(new AcceptedInput(probe.QualifiedItemId, probe.DisplayName, probe.Category, probe.getCategoryName()));
            }

            results = results
                .OrderBy(input => input.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        _acceptedInputCache[machine.QualifiedItemId] = results;
        return results;
    }

    /// <summary>Companion items the machine consumes on every craft (coal-style), from its data.</summary>
    public static IReadOnlyList<RequiredCompanion> EnumerateRequiredCompanions(MachineData? data) =>
        (data?.AdditionalConsumedItems ?? new List<MachineItemAdditionalConsumedItems>())
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemId))
            .Select(item =>
            {
                var probe = ItemRegistry.Create(item.ItemId, 1, allowNull: true);
                return new RequiredCompanion(item.ItemId, probe?.DisplayName ?? item.ItemId, Math.Max(1, item.RequiredCount));
            })
            .ToList();

    /// <summary>Every placed machine in the location, with its tile and data.</summary>
    public IEnumerable<(TileCoord Tile, SObject Machine, MachineData Data)> EnumerateMachines(GameLocation location)
    {
        foreach (var pair in location.objects.Pairs)
        {
            var data = pair.Value.GetMachineData();
            if (data is null)
                continue;

            yield return (new TileCoord((int)pair.Key.X, (int)pair.Key.Y), pair.Value, data);
        }
    }

    /// <summary>
    /// Re-resolve a selected machine against the live world. Returns null when the tile is empty,
    /// no longer a machine, or now holds a different machine type (skip with a dev log — same
    /// contract as managed-crop missing tiles).
    /// </summary>
    public SObject? Resolve(GameLocation location, TileCoord tile, string expectedQualifiedId)
    {
        var vec = new Vector2(tile.X, tile.Y);
        if (!location.objects.TryGetValue(vec, out var obj))
            return null;
        if (obj.GetMachineData() is null)
            return null;
        if (!string.Equals(obj.QualifiedItemId, expectedQualifiedId, StringComparison.Ordinal))
            return null;

        return obj;
    }

    public MachineReadyState Classify(SObject machine)
    {
        if (machine.readyForHarvest.Value && machine.heldObject.Value is not null)
            return MachineReadyState.ReadyToCollect;
        if (machine.heldObject.Value is null)
            return MachineReadyState.Empty;

        return MachineReadyState.Busy;
    }

    /// <summary>
    /// Machines that must never be reloaded regardless of mode: crystalarium-style
    /// (<c>AllowLoadWhenFull</c> would discard the in-progress gem) and incubators (egg→animal,
    /// out of scope). Input-less producers (bee houses, tappers) are not excluded here — they
    /// simply yield no load candidates because no item satisfies their triggers.
    /// </summary>
    public static bool IsReloadable(MachineData data) =>
        data is { AllowLoadWhenFull: false, IsIncubator: false };

    /// <summary>
    /// Build the ordered recipe options for loading <paramref name="machine"/> from the supply the
    /// chest holds, honoring the group's input filter. Returns null when nothing the chest holds can
    /// load the machine (so collect-only machines fall out naturally). Probe-only — mutates nothing.
    /// </summary>
    public MachineLoadCandidate? BuildLoadCandidate(
        MachineRef machineRef,
        SObject machine,
        MachineData data,
        MachineInputFilter filter,
        IReadOnlyDictionary<string, int> chestSupply,
        Farmer who,
        GameLocation location)
    {
        if (!IsReloadable(data))
            return null;

        // When the filter is specific, honor its preference order; otherwise consider whatever the
        // chest holds (deterministic by id so allocation is stable).
        var candidateIds = filter.IsAny
            ? chestSupply.Keys.OrderBy(id => id, StringComparer.Ordinal)
            : filter.AllowedQualifiedIds.Where(chestSupply.ContainsKey);

        var options = new List<RecipeRequirement>();
        foreach (var id in candidateIds)
        {
            if (!filter.Allows(id) || !chestSupply.ContainsKey(id))
                continue;

            var requirement = TryBuildRequirement(machine, data, id, who, location);
            if (requirement is not null)
                options.Add(requirement);
        }

        return options.Count == 0 ? null : new MachineLoadCandidate(machineRef, options);
    }

    private static RecipeRequirement? TryBuildRequirement(
        SObject machine,
        MachineData data,
        string qualifiedId,
        Farmer who,
        GameLocation location)
    {
        // Probe with a generous stack so the count-matching rule (not just ruleIgnoringCount) binds,
        // letting us read the recipe's RequiredCount; the planner clamps to real supply afterwards.
        var probe = ItemRegistry.Create(qualifiedId, 999, allowNull: true);
        if (probe is null)
            return null;

        if (!MachineDataUtility.TryGetMachineOutputRule(
                machine,
                data,
                MachineOutputTrigger.ItemPlacedInMachine,
                probe,
                who,
                location,
                out var rule,
                out var triggerRule,
                out _,
                out _))
        {
            return null;
        }

        if (rule is null || triggerRule is null)
            return null;

        var requiredCount = Math.Max(1, triggerRule.RequiredCount);
        var additional = (data.AdditionalConsumedItems ?? new List<MachineItemAdditionalConsumedItems>())
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemId))
            .Select(item => new AdditionalInput(item.ItemId, Math.Max(1, item.RequiredCount)))
            .ToList();

        return new RecipeRequirement(qualifiedId, requiredCount, additional);
    }
}
