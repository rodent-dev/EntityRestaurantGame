using System.Numerics;
using Engine.Render;

namespace Engine.Ui;

/// <summary>
/// A detached panel the player can pick up and move: titlebar, close box, and
/// an optional row of tabs. Owns its chrome and its position, nothing else —
/// what goes in the body is the caller's business, drawn into the rectangle
/// <see cref="Body"/> hands back.
///
/// Deliberately a plain component rather than a Scene. A scene is a mode; a
/// window is furniture. Several can coexist, and the scene that owns one still
/// decides what blocks the sim.
/// </summary>
public sealed class DragWindow
{
    public const float TitleBarHeight = 11f;

    private readonly string[] _tabs;
    private bool _dragging;
    private Vector2 _grabOffset;

    public string Title { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }

    /// <summary>Index into the tab labels; meaningless when there are no tabs.</summary>
    public int ActiveTab { get; private set; }

    /// <summary>Set once the close box is clicked; the owner decides what that means.</summary>
    public bool CloseRequested { get; private set; }

    public Vector4 Backdrop { get; set; } = new(0.07f, 0.08f, 0.10f, 0.94f);
    public Vector4 Border { get; set; } = new(0.55f, 0.45f, 0.28f, 1f);
    public Vector4 TitleColor { get; set; } = new(0.92f, 0.85f, 0.70f, 1f);
    public Vector4 DimColor { get; set; } = new(0.55f, 0.52f, 0.46f, 1f);

    public DragWindow(string title, Vector2 position, Vector2 size, params string[] tabs)
    {
        Title = title;
        Position = position;
        Size = size;
        _tabs = tabs;
    }

    /// <summary>The content rectangle, below the titlebar and any tab row.</summary>
    public (float X, float Y, float W, float H) Body(SpriteFont font)
    {
        float top = TitleBarHeight + (_tabs.Length > 0 ? font.LineHeight + 5 : 0);
        return (Position.X + 4, Position.Y + top, Size.X - 8, Size.Y - top - 4);
    }

    /// <summary>
    /// Drag, tab-switch, and close handling. Returns true when the pointer is
    /// interacting with this window, so the caller can stop the click falling
    /// through to the world behind it.
    /// </summary>
    public bool Handle(in UiInput input, SpriteFont font, float canvasW, float canvasH)
    {
        // Dragging continues even when the pointer outruns the titlebar —
        // otherwise a fast drag drops the window the moment you overtake it.
        if (_dragging)
        {
            if (input.MouseHeld)
            {
                Position = ClampToCanvas(input.Mouse - _grabOffset, canvasW, canvasH);
                return true;
            }
            _dragging = false;
        }

        bool over = input.MouseOver(Position.X, Position.Y, Size.X, Size.Y);
        if (!input.MousePressed) return over;

        float closeW = font.Measure("x").X + 6;
        if (input.ClickedIn(Position.X + Size.X - closeW, Position.Y, closeW, TitleBarHeight))
        {
            CloseRequested = true;
            return true;
        }

        if (input.ClickedIn(Position.X, Position.Y, Size.X - closeW, TitleBarHeight))
        {
            _dragging = true;
            _grabOffset = input.Mouse - Position;
            return true;
        }

        float tabX = Position.X + 4;
        float tabY = Position.Y + TitleBarHeight + 1;
        for (int i = 0; i < _tabs.Length; i++)
        {
            float w = font.Measure(_tabs[i]).X + 6;
            if (input.ClickedIn(tabX, tabY, w, font.LineHeight + 2))
            {
                ActiveTab = i;
                return true;
            }
            tabX += w + 2;
        }

        return over;
    }

    /// <summary>Cycle tabs from the keyboard, for parity with the mouse.</summary>
    public void NextTab()
    {
        if (_tabs.Length > 0) ActiveTab = (ActiveTab + 1) % _tabs.Length;
    }

    public void SetTab(int index)
    {
        if (index >= 0 && index < _tabs.Length) ActiveTab = index;
    }

    /// <summary>Draw the chrome. The body is the caller's to fill afterward.</summary>
    public void Draw(Renderer2D r, SpriteFont font)
    {
        r.DrawRect(Position.X, Position.Y, Size.X, Size.Y, Backdrop);
        r.DrawRectOutline(Position.X, Position.Y, Size.X, Size.Y, Border);

        r.DrawRect(Position.X, Position.Y, Size.X, TitleBarHeight, new Vector4(0.14f, 0.13f, 0.16f, 1f));
        r.DrawText(font, Title, Position.X + 4, Position.Y + 1, TitleColor);

        float closeW = font.Measure("x").X + 6;
        r.DrawText(font, "x", Position.X + Size.X - closeW + 2, Position.Y + 1, DimColor);

        if (_tabs.Length == 0) return;

        float tabX = Position.X + 4;
        float tabY = Position.Y + TitleBarHeight + 1;
        for (int i = 0; i < _tabs.Length; i++)
        {
            float w = font.Measure(_tabs[i]).X + 6;
            bool active = i == ActiveTab;
            if (active) r.DrawRect(tabX, tabY, w, font.LineHeight + 2, new Vector4(0.20f, 0.18f, 0.22f, 1f));
            r.DrawText(font, _tabs[i], tabX + 3, tabY + 1, active ? TitleColor : DimColor);
            tabX += w + 2;
        }

        r.DrawRect(Position.X + 4, tabY + font.LineHeight + 3, Size.X - 8, 1, DimColor);
    }

    /// <summary>Keep at least the titlebar on screen — a window dragged fully
    /// off the canvas can never be dragged back.</summary>
    private Vector2 ClampToCanvas(Vector2 candidate, float canvasW, float canvasH) =>
        new(Math.Clamp(candidate.X, -Size.X + 24, canvasW - 24),
            Math.Clamp(candidate.Y, 0, canvasH - TitleBarHeight));
}
