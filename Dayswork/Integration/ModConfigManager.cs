using Dayswork.Core.Config;
using StardewModdingAPI;

namespace Dayswork.Integration;

internal sealed class ModConfigManager
{
    private readonly IModHelper _helper;
    private readonly Action<string>? _logWarning;

    public ModConfig Editable { get; private set; }
    public ConfigSnapshot CurrentSnapshot { get; private set; }

    public ModConfigManager(IModHelper helper, Action<string>? logWarning = null)
    {
        _helper = helper;
        _logWarning = logWarning;
        Editable = RuntimeConfigSnapshotMapper.Normalize(helper.ReadConfig<ModConfig>(), _logWarning);
        CurrentSnapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(Editable);
    }

    public void ResetToDefaults()
    {
        Editable = ModConfig.CreateDefaults();
    }

    public void SaveAndPublish()
    {
        Editable = RuntimeConfigSnapshotMapper.Normalize(Editable, _logWarning);
        CurrentSnapshot = RuntimeConfigSnapshotMapper.BuildSnapshot(Editable);
        _helper.WriteConfig(Editable);
    }
}
