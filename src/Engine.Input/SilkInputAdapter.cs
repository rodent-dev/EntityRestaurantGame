using System.Numerics;
using Silk.NET.Input;

namespace Engine.Input;

/// <summary>
/// Bridges Silk.NET input devices into the device-agnostic InputState.
/// Codes are the Silk enum names ("W", "Escape", "Right"), which is what
/// bindings.json speaks.
/// </summary>
public sealed class SilkInputAdapter<TAction> where TAction : struct, Enum
{
    public InputState<TAction> State { get; }

    private Vector2 _lastMouse;
    private bool _hasLastMouse;

    public SilkInputAdapter(IInputContext context, BindingSet<TAction> bindings)
    {
        State = new InputState<TAction>(bindings);

        foreach (var keyboard in context.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) => State.DeviceDown(BindingKind.Key, key.ToString());
            keyboard.KeyUp += (_, key, _) => State.DeviceUp(BindingKind.Key, key.ToString());
        }

        foreach (var mouse in context.Mice)
        {
            mouse.MouseDown += (_, button) => State.DeviceDown(BindingKind.MouseButton, button.ToString());
            mouse.MouseUp += (_, button) => State.DeviceUp(BindingKind.MouseButton, button.ToString());
            mouse.MouseMove += (_, pos) =>
            {
                if (_hasLastMouse) State.FeedMouseDelta(pos - _lastMouse);
                _lastMouse = pos;
                _hasLastMouse = true;
                State.FeedMousePosition(pos);
            };
            State.FeedMousePosition(mouse.Position);
            mouse.Scroll += (_, wheel) => State.FeedScroll(wheel.Y);
        }
    }
}
