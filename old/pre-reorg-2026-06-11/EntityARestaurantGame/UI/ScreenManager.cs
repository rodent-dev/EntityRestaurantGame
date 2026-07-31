namespace EntityARestaurantGame.UI;

public class ScreenManager
{
    private readonly Stack<IScreen> _stack = new();
    private readonly ConsoleRenderer _renderer;

    public ScreenManager(ConsoleRenderer renderer) => _renderer = renderer;

    public void Push(IScreen screen) => _stack.Push(screen);

    public void Run()
    {
        while (_stack.Count > 0)
        {
            var screen = _stack.Peek();
            _renderer.Clear();
            screen.Render(_renderer);

            // Real-time mode: pump the sim between renders until a key arrives.
            if (screen is IRealtimeScreen { IsRunning: true } realtime)
            {
                while (!Console.KeyAvailable)
                {
                    if (realtime.Pump())
                    {
                        _renderer.Clear();
                        screen.Render(_renderer);
                    }
                    Thread.Sleep(33);
                }
            }

            var key = Console.ReadKey(intercept: true);
            var action = _stack.Peek().HandleInput(key);

            Apply(action);
        }
    }

    private void Apply(ScreenAction action)
    {
        switch (action)
        {
            case StayAction:    break;
            case PopAction:     _stack.Pop(); break;
            case QuitAction:    _stack.Clear(); break;
            case PushAction p:  _stack.Push(p.Screen); break;
            case ReplaceAction r:
                _stack.Pop();
                _stack.Push(r.Screen);
                break;
        }
    }
}
