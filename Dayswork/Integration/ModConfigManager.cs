using Dayswork.Core.Config;
using StardewModdingAPI;

namespace Dayswork.Integration;

internal sealed class ModConfigManager
{
    private readonly IModHelper _helper;

    public ModConfig Editable { get; private set; }
    public IConfigSnapshot CurrentSnapshot { get; private set; }

    public ModConfigManager(IModHelper helper)
    {
        _helper = helper;
        Editable = RuntimeConfigSnapshotMapper.Normalize(helper.ReadConfig<ModConfig>());
        CurrentSnapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(Editable);
    }

    public void ResetToDefaults()
    {
        Editable = ModConfig.CreateDefaults();
    }

    public void SaveAndPublish()
    {
        Editable = RuntimeConfigSnapshotMapper.Normalize(Editable);
        CurrentSnapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(Editable);
        _helper.WriteConfig(Editable);
    }
}
