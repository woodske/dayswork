using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI.Layout;

internal sealed class DropdownRow : ILayoutElement
{
    private readonly string _label;
    private readonly string[] _optionLabels;
    private readonly int _selectedIndex;
    private readonly bool _isOpen;
    private readonly Action _onToggle;
    private readonly Action<int> _onChange;

    private Rectangle _rowBounds;
    private Rectangle _buttonBounds;
    private Rectangle[] _optionBounds = Array.Empty<Rectangle>();

    private const int RowHeight = 54;
    private const int OptionHeight = 48;

    public Rectangle OpenBounds { get; private set; }

    public DropdownRow(
        string label,
        string[] optionLabels,
        int selectedIndex,
        bool isOpen,
        Action onToggle,
        Action<int> onChange)
    {
        _label = label;
        _optionLabels = optionLabels;
        _selectedIndex = selectedIndex;
        _isOpen = isOpen;
        _onToggle = onToggle;
        _onChange = onChange;
    }

    public int Measure(int availableWidth) =>
        _isOpen ? RowHeight + OptionHeight * _optionLabels.Length : RowHeight;

    public void Arrange(Rectangle bounds, LayoutContext ctx)
    {
        _rowBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, RowHeight);

        var splitX = bounds.X + bounds.Width / 2;
        _buttonBounds = new Rectangle(splitX, bounds.Y, bounds.Right - splitX, RowHeight);

        ctx.Register(_buttonBounds, "idle_dropdown", string.Empty, () =>
        {
            Game1.playSound("smallSelect");
            _onToggle();
        });

        if (_isOpen)
        {
            _optionBounds = new Rectangle[_optionLabels.Length];
            for (var i = 0; i < _optionLabels.Length; i++)
            {
                _optionBounds[i] = new Rectangle(
                    _buttonBounds.X, _buttonBounds.Bottom + OptionHeight * i,
                    _buttonBounds.Width, OptionHeight);
                var idx = i;
                ctx.Register(_optionBounds[i], $"idle_option_{i}", string.Empty, () =>
                {
                    Game1.playSound("smallSelect");
                    _onChange(idx);
                });
            }

            OpenBounds = new Rectangle(
                _buttonBounds.X, _buttonBounds.Y,
                _buttonBounds.Width, RowHeight + OptionHeight * _optionLabels.Length);
        }
        else
        {
            _optionBounds = Array.Empty<Rectangle>();
            OpenBounds = _buttonBounds;
        }
    }

    public void Draw(SpriteBatch b)
    {
        Utility.drawTextWithShadow(
            b, _label, Game1.smallFont,
            new Vector2(
                _rowBounds.X + 8,
                _rowBounds.Y + (_rowBounds.Height - (int)Game1.smallFont.MeasureString(_label).Y) / 2),
            Game1.textColor);

        IClickableMenu.drawTextureBox(b, _buttonBounds.X, _buttonBounds.Y, _buttonBounds.Width, _buttonBounds.Height, Color.White);

        var selectedText = _optionLabels[_selectedIndex];
        var selectedSize = Game1.smallFont.MeasureString(selectedText);
        Utility.drawTextWithShadow(
            b, selectedText, Game1.smallFont,
            new Vector2(
                _buttonBounds.X + 16,
                _buttonBounds.Y + (_buttonBounds.Height - (int)selectedSize.Y) / 2),
            Game1.textColor);

        const string arrow = "▼";
        var arrowSize = Game1.smallFont.MeasureString(arrow);
        Utility.drawTextWithShadow(
            b, arrow, Game1.smallFont,
            new Vector2(
                _buttonBounds.Right - 16 - (int)arrowSize.X,
                _buttonBounds.Y + (_buttonBounds.Height - (int)arrowSize.Y) / 2),
            Game1.textColor);

        if (!_isOpen)
            return;

        for (var i = 0; i < _optionBounds.Length; i++)
        {
            var optRect = _optionBounds[i];
            IClickableMenu.drawTextureBox(
                b, optRect.X, optRect.Y, optRect.Width, optRect.Height,
                i == _selectedIndex ? Color.LightSkyBlue : Color.White);

            var optText = _optionLabels[i];
            var optSize = Game1.smallFont.MeasureString(optText);
            Utility.drawTextWithShadow(
                b, optText, Game1.smallFont,
                new Vector2(
                    optRect.X + 16,
                    optRect.Y + (optRect.Height - (int)optSize.Y) / 2),
                Game1.textColor);
        }
    }
}
