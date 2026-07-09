using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class PreferencesMenu : LayoutMenu
{
    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onBack;
    private bool _idleDropdownOpen;
    private DropdownRow? _dropdown;

    internal PreferencesMenu(ContractDraft draft, Action<ContractDraft> onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: () => onBack(draft))
    {
        _draft = draft;
        _onBack = onBack;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout()
    {
        _dropdown = new DropdownRow(
            label: I18nHelper.Get("ui.preferences.idle_task_label"),
            optionLabels: new[]
            {
                I18nHelper.Get("ui.preferences.idle_task.none"),
                I18nHelper.Get("ui.preferences.idle_task.manage_machines")
            },
            selectedIndex: (int)_draft.Preferences.IdleTask,
            isOpen: _idleDropdownOpen,
            onToggle: () =>
            {
                _idleDropdownOpen = !_idleDropdownOpen;
                Rebuild();
            },
            onChange: idx =>
            {
                _idleDropdownOpen = false;
                _draft.Preferences = _draft.Preferences with { IdleTask = (IdleTaskKind)idx };
                _draft.MarkDirty();
                Rebuild();
            });

        return new PageShell(
            title: I18nHelper.Get("ui.preferences.title"),
            onBack: BackAction!,
            content: new VStack(8,
                new MenuButton(
                    I18nHelper.Get("ui.preferences.worker_name", new
                    {
                        name = Worker.FarmhandNpc.DisplayNameFor(_draft.Preferences.WorkerName),
                    }),
                    OpenWorkerNamingMenu,
                    textAlign: HAlign.Left),
                new ToggleRow(
                    I18nHelper.Get("ui.preferences.avoid_blue_grass"),
                    _draft.Preferences.AvoidBlueGrass,
                    () =>
                    {
                        _draft.Preferences = _draft.Preferences with { AvoidBlueGrass = !_draft.Preferences.AvoidBlueGrass };
                        _draft.MarkDirty();
                        Rebuild();
                    }),
                _dropdown,
                new Label(I18nHelper.Get("ui.preferences.idle_task_description"))));
    }

    // Vanilla NamingMenu as a modal step (there's no text-input layout element): done-naming
    // writes the draft and reopens this menu; its random button doubles as a name generator.
    private void OpenWorkerNamingMenu()
    {
        Game1.activeClickableMenu = new NamingMenu(
            name =>
            {
                _draft.Preferences = _draft.Preferences with { WorkerName = name?.Trim() ?? "" };
                _draft.MarkDirty();
                Game1.activeClickableMenu = new PreferencesMenu(_draft, _onBack);
            },
            I18nHelper.Get("ui.preferences.worker_name_prompt"),
            string.IsNullOrWhiteSpace(_draft.Preferences.WorkerName) ? null : _draft.Preferences.WorkerName);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_idleDropdownOpen && _dropdown is { } d && !d.OpenBounds.Contains(x, y))
        {
            _idleDropdownOpen = false;
            Rebuild();
        }

        base.receiveLeftClick(x, y, playSound);
    }
}
