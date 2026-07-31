using System.Numerics;
using Engine.Render;

namespace Engine.Ui;

public sealed class MenuItem
{
    public string Label { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public Func<SceneAction> Execute { get; init; } = () => SceneAction.Stay;
}

/// <summary>
/// Vertical keyboard-driven option list — the windowed MenuOption. Up/Down
/// wraps and skips disabled items; Confirm executes the selection.
/// </summary>
public sealed class MenuList
{
    private readonly List<MenuItem> _items;
    private int _scrollTop;

    public int Selected { get; private set; }

    /// <summary>
    /// How many rows fit before the list scrolls. Zero means "show them all",
    /// which is what fixed menus want. Any list built from world contents —
    /// party members, valid targets, a settlement's residents — must set this,
    /// or a big enough world silently runs off the bottom of the canvas.
    /// </summary>
    public int VisibleRows { get; set; }

    public int Count => _items.Count;

    public Vector4 NormalColor { get; set; } = new(0.78f, 0.74f, 0.66f, 1f);
    public Vector4 SelectedColor { get; set; } = new(1f, 0.90f, 0.55f, 1f);
    public Vector4 DisabledColor { get; set; } = new(0.42f, 0.40f, 0.38f, 1f);

    public MenuList(IEnumerable<MenuItem> items)
    {
        _items = [.. items];
        if (_items.Count == 0)
            throw new ArgumentException("MenuList needs at least one item.", nameof(items));
        while (Selected < _items.Count - 1 && !_items[Selected].IsEnabled)
            Selected++;
    }

    public SceneAction Handle(in UiInput input)
    {
        if (input.Up) Move(-1);
        if (input.Down) Move(+1);
        if (input.Confirm && _items[Selected].IsEnabled)
            return _items[Selected].Execute();
        return SceneAction.Stay;
    }

    private void Move(int direction)
    {
        for (int step = 1; step <= _items.Count; step++)
        {
            int candidate = (Selected + direction * step % _items.Count + _items.Count) % _items.Count;
            if (_items[candidate].IsEnabled)
            {
                Selected = candidate;
                ScrollIntoView();
                return;
            }
        }
    }

    /// <summary>Keep the highlight inside the visible window, including after
    /// the wrap from last item back to first.</summary>
    private void ScrollIntoView()
    {
        if (VisibleRows <= 0 || _items.Count <= VisibleRows)
        {
            _scrollTop = 0;
            return;
        }
        if (Selected < _scrollTop) _scrollTop = Selected;
        else if (Selected >= _scrollTop + VisibleRows) _scrollTop = Selected - VisibleRows + 1;
        _scrollTop = Math.Clamp(_scrollTop, 0, _items.Count - VisibleRows);
    }

    /// <summary>Draw items top-down, centered on <paramref name="centerX"/>.</summary>
    public void Draw(Renderer2D r, SpriteFont font, float centerX, float y, float spacing = 0)
    {
        if (spacing <= 0) spacing = font.LineHeight + 3;

        int window = VisibleRows > 0 ? Math.Min(VisibleRows, _items.Count) : _items.Count;
        int first = VisibleRows > 0 ? _scrollTop : 0;

        // Ellipsis markers rather than arrows: at 480x270 a caret is a smudge,
        // but "..." reads as "there is more" at a glance.
        if (first > 0)
        {
            r.DrawTextCentered(font, "...", centerX, y, DisabledColor);
            y += spacing;
        }

        for (int i = first; i < first + window; i++)
        {
            var item = _items[i];
            var color = !item.IsEnabled ? DisabledColor
                      : i == Selected ? SelectedColor
                      : NormalColor;

            r.DrawTextCentered(font, item.Label, centerX, y, color);
            if (i == Selected)
            {
                float halfWidth = font.Measure(item.Label).X / 2f;
                r.DrawText(font, ">", centerX - halfWidth - font.Measure("> ").X, y, SelectedColor);
            }
            y += spacing;
        }

        if (first + window < _items.Count)
            r.DrawTextCentered(font, "...", centerX, y, DisabledColor);
    }
}
