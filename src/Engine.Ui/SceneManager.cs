using Engine.Render;

namespace Engine.Ui;

/// <summary>
/// The scene controller: a stack of scenes over the 3D view. Input goes to the
/// top scene only; drawing starts at the deepest scene still visible through
/// overlays. Same pause trick as the console ScreenManager — while any
/// sim-blocking scene is on the stack, the host simply stops feeding the sim.
/// </summary>
public sealed class SceneManager
{
    private readonly List<Scene> _stack = [];
    private readonly UiContext _ctx;

    /// <summary>Raised when a scene returns SceneAction.Quit — the host decides what quitting means.</summary>
    public event Action? QuitRequested;

    public SceneManager(UiContext ctx) => _ctx = ctx;

    public bool IsEmpty => _stack.Count == 0;
    public bool SimPaused => _stack.Any(s => s.BlocksSim);
    /// <summary>The scene currently receiving input, or null when the stack is empty.</summary>
    public Scene? Top => _stack.Count == 0 ? null : _stack[^1];

    public void Push(Scene scene)
    {
        scene.Attach(_ctx);
        _stack.Add(scene);
    }

    /// <summary>Route input to the top scene and apply whatever it asks for.</summary>
    public void Update(double dt, in UiInput input)
    {
        if (_stack.Count == 0) return;
        Apply(_stack[^1].Update(dt, input));
    }

    /// <summary>Draw visible scenes bottom-up. Owns its own Begin/End pass.</summary>
    public void Draw()
    {
        if (_stack.Count == 0) return;

        int first = _stack.Count - 1;
        while (first > 0 && _stack[first].IsOverlay) first--;

        _ctx.Renderer.Begin(_ctx.Width, _ctx.Height);
        for (int i = first; i < _stack.Count; i++)
            _stack[i].Draw(_ctx.Renderer);
        _ctx.Renderer.End();
    }

    private void Apply(SceneAction action)
    {
        switch (action)
        {
            case StaySceneAction: break;
            case PopSceneAction: _stack.RemoveAt(_stack.Count - 1); break;
            case PushSceneAction p: Push(p.Scene); break;
            case ReplaceSceneAction r:
                _stack.RemoveAt(_stack.Count - 1);
                Push(r.Scene);
                break;
            case QuitSceneAction:
                _stack.Clear();
                QuitRequested?.Invoke();
                break;
        }
    }
}
