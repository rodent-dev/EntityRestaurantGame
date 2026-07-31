using Engine.Render;

namespace Engine.Ui;

/// <summary>
/// One screen of 2D interface — the windowed counterpart of the console
/// IScreen. Scenes live on the SceneManager's stack; the top scene gets input,
/// and its Update returns a SceneAction telling the manager what to do next.
/// </summary>
public abstract class Scene
{
    /// <summary>While any scene with this set is on the stack, the host stops pumping the sim.</summary>
    public virtual bool BlocksSim => true;

    /// <summary>Overlay scenes let the scene beneath them keep drawing (the 3D world always draws).</summary>
    public virtual bool IsOverlay => false;

    /// <summary>Valid from OnLoad onward.</summary>
    protected UiContext Ctx { get; private set; } = null!;

    internal void Attach(UiContext ctx)
    {
        Ctx = ctx;
        OnLoad();
    }

    /// <summary>Called once when pushed, with Ctx available — load textures here.</summary>
    protected virtual void OnLoad() { }

    public abstract SceneAction Update(double dt, in UiInput input);

    /// <summary>Called between Renderer2D.Begin/End — just draw.</summary>
    public abstract void Draw(Renderer2D r);
}

// ── SceneAction sealed hierarchy (mirror of the console ScreenAction) ──────

public abstract class SceneAction
{
    public static readonly SceneAction Stay = new StaySceneAction();
    public static readonly SceneAction Pop = new PopSceneAction();
    public static readonly SceneAction Quit = new QuitSceneAction();

    public static SceneAction Push(Scene scene) => new PushSceneAction(scene);
    public static SceneAction Replace(Scene scene) => new ReplaceSceneAction(scene);
}

public sealed class StaySceneAction : SceneAction { }
public sealed class PopSceneAction : SceneAction { }
public sealed class QuitSceneAction : SceneAction { }
public sealed class PushSceneAction : SceneAction { public Scene Scene { get; } public PushSceneAction(Scene s) => Scene = s; }
public sealed class ReplaceSceneAction : SceneAction { public Scene Scene { get; } public ReplaceSceneAction(Scene s) => Scene = s; }
