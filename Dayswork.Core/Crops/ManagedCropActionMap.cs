namespace Dayswork.Core.Crops;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

/// <summary>
/// Pure, total mapping from a managed-crop tile action to its capability tool and energy kind
///. Planting and fertilizing are not tool-gated (they gate on item availability);
/// clearing debris resolves its tool from the live debris type, supplied by the runtime.
/// </summary>
public static class ManagedCropActionMap
{
    /// <summary>The energy cost kind charged for one beat of this action.</summary>
    public static WorkActionKind EnergyKind(ManagedCropActionKind kind, WorkerTool debrisTool = WorkerTool.None) =>
        kind switch
        {
            ManagedCropActionKind.Harvest => WorkActionKind.HarvestCrop,
            ManagedCropActionKind.ClearDebris => debrisTool switch
            {
                WorkerTool.Axe => WorkActionKind.AxeSwing,
                WorkerTool.Pickaxe => WorkActionKind.PickaxeSwing,
                _ => WorkActionKind.ScytheSwing,
            },
            ManagedCropActionKind.Till => WorkActionKind.HoeSwing,
            ManagedCropActionKind.Fertilize => WorkActionKind.ApplyFertilizer,
            ManagedCropActionKind.PlantSeed => WorkActionKind.PlantSeed,
            ManagedCropActionKind.Water => WorkActionKind.WaterTile,
            _ => WorkActionKind.ScytheSwing,
        };

    /// <summary>The tool whose animation/capability applies to this action.</summary>
    public static WorkerTool Tool(ManagedCropActionKind kind, WorkerTool debrisTool = WorkerTool.None) =>
        kind switch
        {
            ManagedCropActionKind.Harvest => WorkerTool.None,
            ManagedCropActionKind.ClearDebris => debrisTool,
            ManagedCropActionKind.Till => WorkerTool.Hoe,
            ManagedCropActionKind.Fertilize => WorkerTool.None,
            ManagedCropActionKind.PlantSeed => WorkerTool.None,
            ManagedCropActionKind.Water => WorkerTool.WateringCan,
            _ => WorkerTool.None,
        };

    /// <summary>
    /// True when the action requires a tool that may be missing/under-leveled (so a failed
    /// capability check skips the action). Planting, fertilizing, and harvesting gate only on
    /// item/world state, never on a tool.
    /// </summary>
    public static bool IsToolGated(ManagedCropActionKind kind) =>
        kind is ManagedCropActionKind.ClearDebris
             or ManagedCropActionKind.Till
             or ManagedCropActionKind.Water;
}
