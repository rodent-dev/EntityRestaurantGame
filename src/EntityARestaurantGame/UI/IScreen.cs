namespace EntityARestaurantGame.UI;

public interface IScreen
{
    string Title { get; }
    void Render(ConsoleRenderer renderer);
    ScreenAction HandleInput(ConsoleKeyInfo key);
}

/// <summary>
/// A screen that advances the simulation in real time while it is on top of
/// the stack and no key is pending. Pushing any non-realtime screen above it
/// pauses the world for free — the accumulator simply stops being fed.
/// </summary>
public interface IRealtimeScreen : IScreen
{
    bool IsRunning { get; }

    /// <summary>Feed elapsed real time to the sim. True if visible state changed.</summary>
    bool Pump();
}

// ── ScreenAction sealed hierarchy ──────────────────────────────────────────

public abstract class ScreenAction
{
    public static readonly ScreenAction Stay = new StayAction();
    public static readonly ScreenAction Pop  = new PopAction();
    public static readonly ScreenAction Quit = new QuitAction();

    public static ScreenAction Push(IScreen screen)    => new PushAction(screen);
    public static ScreenAction Replace(IScreen screen) => new ReplaceAction(screen);
}

public sealed class StayAction    : ScreenAction { }
public sealed class PopAction     : ScreenAction { }
public sealed class QuitAction    : ScreenAction { }
public sealed class PushAction    : ScreenAction { public IScreen Screen { get; } public PushAction(IScreen s) => Screen = s; }
public sealed class ReplaceAction : ScreenAction { public IScreen Screen { get; } public ReplaceAction(IScreen s) => Screen = s; }
