using Dayswork.Integration;
using Dayswork.UI.Layout;

namespace Dayswork.UI;

internal sealed class PreferencesMenu : LayoutMenu
{
    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft> _onBack;

    internal PreferencesMenu(ContractDraft draft, Action<ContractDraft> onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height, onBack: () => onBack(draft))
    {
        _draft = draft;
        _onBack = onBack;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.preferences.title"),
            onBack: BackAction!,
            content: new VStack(8,
                new ToggleRow(
                    I18nHelper.Get("ui.preferences.avoid_blue_grass"),
                    _draft.Preferences.AvoidBlueGrass,
                    () =>
                    {
                        _draft.Preferences = _draft.Preferences with { AvoidBlueGrass = !_draft.Preferences.AvoidBlueGrass };
                        _draft.MarkDirty();
                        Rebuild();
                    })));
}
