using System.Numerics;

namespace Engine.Ui;

/// <summary>
/// Device-free per-frame UI intents (pressed-this-frame semantics). The game
/// maps its own action enum onto this each frame, so Engine.Ui never learns
/// about keyboards, bindings, or game actions.
///
/// <para>Mouse position is in CANVAS pixels, not window pixels — the host owns
/// that conversion. Scenes lay themselves out in canvas space, so they can
/// hit-test against their own rectangles without knowing the window size or
/// the upscale factor.</para>
/// </summary>
public readonly record struct UiInput(
    bool Up,
    bool Down,
    bool Confirm,
    bool Cancel,
    Vector2 Mouse = default,
    bool MousePressed = false,
    bool MouseHeld = false,
    bool MouseReleased = false)
{
    /// <summary>Is the pointer inside this canvas-space rectangle?</summary>
    public bool MouseOver(float x, float y, float width, float height) =>
        Mouse.X >= x && Mouse.X < x + width &&
        Mouse.Y >= y && Mouse.Y < y + height;

    /// <summary>Was a click begun inside this rectangle this frame?</summary>
    public bool ClickedIn(float x, float y, float width, float height) =>
        MousePressed && MouseOver(x, y, width, height);
}
