using Dayswork.Core.Domain;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

/// <summary>Per-tier energy + price for an Energy option card. Computed by the coordinator.</summary>
internal sealed record EnergyTierOption(EnergyTier Tier, int? Energy, int? Price);

// Energy page — lists all three energy tiers as selectable cards (replacing the old arrow selector).
// Selecting a card sets ContractDraft.Tier; Back returns to the hub.
internal sealed class EnergyMenu : IClickableMenu
{
    private const int CardHeight = 110;
    private const int CardGap = 16;

    private readonly ContractDraft _draft;
    private readonly IReadOnlyList<EnergyTierOption> _options;
    private readonly Action<ContractDraft, EnergyTier> _onSelectTier;
    private readonly Action<ContractDraft> _onBack;

    private readonly List<ClickableComponent> _cards = new();
    private ClickableComponent _backBtn = null!;

    public EnergyMenu(
        ContractDraft draft,
        IReadOnlyList<EnergyTierOption> options,
        Action<ContractDraft, EnergyTier> onSelectTier,
        Action<ContractDraft> onBack)
        : base(0, 0, ContractMenuLayout.Width, ContractMenuLayout.Height)
    {
        _draft = draft;
        _options = options;
        _onSelectTier = onSelectTier;
        _onBack = onBack;

        var topLeft = ContractMenuLayout.GetTopLeft(width, height);
        xPositionOnScreen = (int)topLeft.X;
        yPositionOnScreen = (int)topLeft.Y;

        BuildComponents();
        populateClickableComponentList();
    }

    private void BuildComponents()
    {
        _cards.Clear();

        var cardX = xPositionOnScreen + 48;
        var cardW = width - 96;
        var startY = yPositionOnScreen + 200;

        for (var i = 0; i < _options.Count; i++)
        {
            _cards.Add(new ClickableComponent(
                new Rectangle(cardX, startY + i * (CardHeight + CardGap), cardW, CardHeight),
                _options[i].Tier.ToString(),
                string.Empty)
            {
                myID = 500 + i,
                upNeighborID = i > 0 ? 500 + i - 1 : -1,
                downNeighborID = i < _options.Count - 1 ? 500 + i + 1 : 901,
            });
        }

        _backBtn = new ClickableComponent(
            new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + height - 70, 170, 56),
            "Back",
            I18nHelper.Get("ui.common.back_btn"))
        {
            myID = 901,
            upNeighborID = _options.Count > 0 ? 500 + _options.Count - 1 : -1,
        };
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            if (!_cards[i].bounds.Contains(x, y))
                continue;

            Game1.playSound("smallSelect");
            _onSelectTier(_draft, _options[i].Tier);
            return;
        }

        if (_backBtn.bounds.Contains(x, y))
            _onBack(_draft);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B)
        {
            _onBack(_draft);
            return;
        }

        base.receiveGamePadButton(b);
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.AddRange(_cards);
        allClickableComponents.Add(_backBtn);
    }

    public override void snapToDefaultClickableComponent()
    {
        var selected = _options.ToList().FindIndex(option => option.Tier == _draft.Tier);
        currentlySnappedComponent = selected >= 0 && selected < _cards.Count ? _cards[selected] : _backBtn;
        snapCursorToCurrentSnappedComponent();
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Utility.drawTextWithShadow(
            b,
            I18nHelper.Get("ui.energy.title"),
            Game1.dialogueFont,
            new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 20),
            Game1.textColor);

        var wrappedDescription = Game1.parseText(I18nHelper.Get("ui.energy.description"), Game1.smallFont, width - 96);
        Utility.drawTextWithShadow(
            b,
            wrappedDescription,
            Game1.smallFont,
            new Vector2(xPositionOnScreen + 48, yPositionOnScreen + 72),
            Game1.textColor);

        for (var i = 0; i < _cards.Count; i++)
            DrawCard(b, _cards[i], _options[i]);

        DrawButton(b, _backBtn);
        drawMouse(b);
    }

    private void DrawCard(SpriteBatch b, ClickableComponent card, EnergyTierOption option)
    {
        var selected = option.Tier == _draft.Tier;
        var cardColor = selected ? Color.LightSkyBlue : Color.White;
        drawTextureBox(b, card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height, cardColor);

        var tierName = I18nHelper.Get($"ui.summary.tier.{TierKey(option.Tier)}");
        Utility.drawTextWithShadow(
            b,
            tierName,
            Game1.smallFont,
            new Vector2(card.bounds.X + 20, card.bounds.Y + 20),
            selected ? Color.DarkBlue : Game1.textColor);

        if (option.Energy is { } energy && option.Price is { } price)
        {
            var detail = I18nHelper.Get("ui.energy.option_detail", new { energy, price });
            Utility.drawTextWithShadow(
                b,
                detail,
                Game1.smallFont,
                new Vector2(card.bounds.X + 20, card.bounds.Y + 58),
                Game1.textColor);
        }
    }

    private static string TierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
        _ => tier.ToString(),
    };

    private static void DrawButton(SpriteBatch b, ClickableComponent btn)
    {
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(
            b,
            btn.label,
            Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width - (int)textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)textSize.Y) / 2),
            Game1.textColor);
    }
}
