using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Integration;
using Dayswork.Orchestration;
using Dayswork.UI.Layout;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.UI;

internal sealed class MachineGroupEditorMenu : LayoutMenu
{
    private const int ButtonHeight = 46;
    private const int LabelColW = 200;
    private const int ValueButtonW = 320;

    private static readonly Color SecondaryTextColor = new(96, 72, 48);
    private static readonly Color HeaderColor = new(80, 60, 40);

    private readonly ContractDraft _draft;
    private readonly MachineGroupDraft _group;
    private readonly Action<ContractDraft> _onBack;
    private readonly Action<string> _onPickType;
    private readonly Action<string> _onPickInputChest;
    private readonly Action<string> _onPickInputFilter;
    private readonly Action<string> _onPickOutput;
    private readonly Action<string> _onSelectMachines;

    public MachineGroupEditorMenu(
        ContractDraft draft,
        MachineGroupDraft group,
        Action<ContractDraft> onBack,
        Action<string> onPickType,
        Action<string> onPickInputChest,
        Action<string> onPickInputFilter,
        Action<string> onPickOutput,
        Action<string> onSelectMachines)
        : base(ContractMenuLayout.ManageCropsWidth, ContractMenuLayout.Height,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _group = group;
        _onBack = onBack;
        _onPickType = onPickType;
        _onPickInputChest = onPickInputChest;
        _onPickInputFilter = onPickInputFilter;
        _onPickOutput = onPickOutput;
        _onSelectMachines = onSelectMachines;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout()
    {
        var hasType = _group.HasType;
        var data = hasType ? MachineReader.GetMachineDataForType(_group.MachineType!) : null;
        var inputCapable = MachineReader.AcceptsInput(data);

        // An input-less machine type (bee house, tapper) can only collect — pin the mode so pricing
        // and the shift engine see a consistent group.
        if (hasType && !inputCapable && _group.Mode == MachineGroupMode.CollectAndReload)
            _group.Mode = MachineGroupMode.CollectOnly;

        var reload = inputCapable && _group.Mode == MachineGroupMode.CollectAndReload;

        // 1. Machine type — the starting choice that scopes everything below it.
        var rows = new List<ILayoutElement>
        {
            BuildButtonRow(
                I18nHelper.Get("ui.manage_machines.type_label"),
                MachineTypeLabel(),
                () => _onPickType(_group.Id),
                enabled: true),
            new Spacer(16),
        };

        if (!hasType)
        {
            rows.Add(new Label(I18nHelper.Get("ui.manage_machines.choose_type_hint"), color: SecondaryTextColor, wrap: true));
        }
        else
        {
            // 2. Reload toggle (or a collect-only note when the type takes no input).
            if (inputCapable)
            {
                rows.Add(new ToggleRow(
                    I18nHelper.Get("ui.manage_machines.reload_toggle"),
                    reload,
                    () =>
                    {
                        _group.Mode = reload ? MachineGroupMode.CollectOnly : MachineGroupMode.CollectAndReload;
                        Rebuild();
                    }));
                rows.Add(new Spacer(8));
                rows.Add(new Label(I18nHelper.Get("ui.manage_machines.reload_help"), color: SecondaryTextColor, wrap: true));
            }
            else
            {
                rows.Add(new Label(I18nHelper.Get("ui.manage_machines.collect_only_no_input"), color: SecondaryTextColor, wrap: true));
            }

            rows.Add(new Spacer(16));

            // 3. Select machines on the map (restricted to the chosen type).
            rows.Add(new MenuButton(
                I18nHelper.Get("ui.manage_machines.select_machines_btn"),
                () => _onSelectMachines(_group.Id),
                fixedWidth: 360,
                height: ButtonHeight));
            rows.Add(new Spacer(8));
            rows.Add(new Label(
                I18nHelper.Get("ui.manage_machines.machine_count", new { count = _group.Machines.Count }),
                color: SecondaryTextColor));

            // 4 + 5. Inputs (data-derived) then input chest — only when reloading.
            if (inputCapable)
            {
                rows.Add(new Spacer(16));
                rows.Add(BuildButtonRow(
                    I18nHelper.Get("ui.manage_machines.input_filter_label"),
                    InputFilterLabel(),
                    () => _onPickInputFilter(_group.Id),
                    enabled: reload && _group.HasAnyMachine));

                rows.Add(new Spacer(10));
                rows.Add(BuildButtonRow(
                    I18nHelper.Get("ui.manage_machines.input_chest_label"),
                    InputChestLabel(),
                    () => _onPickInputChest(_group.Id),
                    enabled: reload));
            }
        }

        // 6. Output destination (always available).
        rows.Add(new Spacer(16));
        rows.Add(BuildButtonRow(
            I18nHelper.Get("ui.manage_machines.output_label"),
            ManageMachinesMenu.OutputLabel(_group.OutputDestination),
            () => _onPickOutput(_group.Id),
            enabled: true));

        if (_group.RequiresInputChest)
        {
            rows.Add(new Spacer(8));
            rows.Add(new Label(I18nHelper.Get("ui.manage_machines.needs_input_chest"), color: new Color(178, 34, 34)));
        }

        return new PageShell(
            title: I18nHelper.Get("ui.manage_machines.editor_title"),
            description: I18nHelper.Get("ui.manage_machines.editor_help"),
            onBack: () => _onBack(_draft),
            content: new VStack(0, rows.ToArray()));
    }

    private string MachineTypeLabel() =>
        _group.HasType
            ? ItemRegistry.GetData(_group.MachineType!)?.DisplayName ?? _group.MachineType!
            : I18nHelper.Get("ui.manage_machines.type_none");

    private static ILayoutElement BuildButtonRow(string label, string value, Action onClick, bool enabled) =>
        new HStack(14,
            HStack.Fixed(new Label(label, color: HeaderColor), LabelColW),
            HStack.Fixed(
                new MenuButton(value, onClick, enabled: enabled, fixedWidth: ValueButtonW, height: ButtonHeight, textAlign: HAlign.Left),
                ValueButtonW));

    private string InputChestLabel() =>
        _group.InputChest is { } chestRef
            ? I18nHelper.Get("ui.manage_machines.chest_at", new { x = chestRef.Tile.X, y = chestRef.Tile.Y })
            : I18nHelper.Get("ui.manage_machines.chest_none");

    private string InputFilterLabel() =>
        _group.IsAnyInput
            ? I18nHelper.Get("ui.manage_machines.input_any")
            : I18nHelper.Get("ui.manage_machines.input_specific", new { count = _group.AllowedInputIds.Count });
}
