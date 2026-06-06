using Dayswork.Core.Domain;
using Dayswork.Integration;
using Dayswork.UI.Layout;
using StardewValley;

namespace Dayswork.UI;

internal sealed class ScheduleMenu : LayoutMenu
{
    private readonly ContractDraft _draft;
    private readonly Action<ContractDraft, ContractSchedule> _onScheduleChanged;

    internal ScheduleMenu(
        ContractDraft draft,
        Action<ContractDraft, ContractSchedule> onScheduleChanged,
        Action<ContractDraft> onBack)
        : base(ContractMenuLayout.Width, ContractMenuLayout.Height,
            onBack: () => onBack(draft))
    {
        _draft = draft;
        _onScheduleChanged = onScheduleChanged;
        Rebuild();
    }

    protected override ILayoutElement BuildLayout() =>
        new PageShell(
            title: I18nHelper.Get("ui.schedule.title"),
            onBack: BackAction!,
            content: new HStack(40,
                HStack.Fill(BuildCard(
                    ContractSchedule.OneTime,
                    I18nHelper.Get("ui.schedule.one_time"),
                    I18nHelper.Get("ui.schedule.one_time_description"))),
                HStack.Fill(BuildCard(
                    ContractSchedule.Recurring,
                    I18nHelper.Get("ui.schedule.recurring"),
                    I18nHelper.Get("ui.schedule.recurring_description")))));

    private ILayoutElement BuildCard(ContractSchedule schedule, string label, string description) =>
        new SelectableCard(
            content: new VStack(8,
                new Label(label,
                    color: _draft.Schedule == schedule
                        ? Microsoft.Xna.Framework.Color.DarkBlue
                        : Game1.textColor),
                new Label(description, wrap: true)),
            isSelected: _draft.Schedule == schedule,
            onClick: () => _onScheduleChanged(_draft, schedule),
            height: 200);
}
