using System.Diagnostics;
using System.Numerics;
using EntityObjects;

namespace EntityARestaurantGame.UI.Screens;

public class WorldScreen : IRealtimeScreen
{
    private readonly EntityController _controller;
    private readonly Func<Entity> _spawnNobody;
    private readonly Stopwatch _timer = new();

    public WorldScreen(EntityController controller, Func<Entity> spawnNobody)
    {
        _controller = controller;
        _spawnNobody = spawnNobody;
    }

    public string Title => "World";

    public bool IsRunning { get; private set; }

    private bool FavoredIsDead =>
        _controller.FavoredEntityId != null && _controller.FavoredEntity == null;

    public bool Pump()
    {
        // Cap the delta so time doesn't lurch after a pause or a pushed screen.
        double dt = Math.Min(_timer.Elapsed.TotalSeconds, 0.25);
        _timer.Restart();
        bool changed = _controller.Pump(dt) > 0;

        // Your pawn dying is the one thing the world stops for — on YOUR screen.
        if (changed && FavoredIsDead) IsRunning = false;
        return changed;
    }

    public void Render(ConsoleRenderer renderer)
    {
        string mode = IsRunning ? "RUNNING" : "PAUSED";
        renderer.DrawStatusBar($"Entity Influence Engine  |  Tick: {_controller.CurrentTick}  |  {mode}");

        var lines = _controller.ActiveEntities.Select(e =>
        {
            string mark    = e.ID == _controller.FavoredEntityId ? "*" : " ";
            string hp      = $"HP {e.GetCurrentHealth():F0}/{e.GetMaxHealth():F0}";
            string hunger  = $"HGR {e.Needs.Hunger}";
            string mood    = $"MOD {e.Needs.Mood}";
            string loc     = _controller.LocationTag(e);
            var last       = _controller.GetDecisionHistory(e.ID).LastOrDefault();
            string action  = last is not null ? $"→ {last.ActionName} {last.TargetName}" : "→ idle";
            return $"{mark}{e.DisplayName,-9} {hp,-13} {hunger,-7} {mood,-7} {loc,-13} {action}";
        }).Concat(_controller.InactiveEntities.Select(e =>
            $" {e.DisplayName,-9} {(e.IsAlive ? "[inactive]" : "[DEAD]")}"));

        renderer.DrawPanel("Entities  (* = your pawn)", lines);

        // Debug view only — reads sim truth (Vector3 positions / AABB locations).
        // Disposable the day a real 3D renderer replaces it.
        if (_controller.Locations != null)
            renderer.DrawPanel("Map (debug)", BuildMap());

        var recent = _controller.Signals.TakeLast(6).ToList();
        renderer.DrawPanel("Events", recent.Count == 0
            ? ["(nothing has happened yet)"]
            : recent.Select(s => $"t{s.Tick,5}  {s.Message}"));

        if (FavoredIsDead)
        {
            renderer.WriteLine();
            renderer.WriteLine("  ☠  Your pawn has fallen. The world persists. Press any key.");
            return;
        }

        renderer.DrawOptions(BuildOptions());
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Quit");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
            return ScreenAction.Quit;

        if (FavoredIsDead)
            return ScreenAction.Push(new StartAnewScreen(_controller, _spawnNobody));

        if (key.Key == ConsoleKey.Spacebar)
        {
            _controller.AdvanceToNextBeat();
            return ScreenAction.Stay;
        }

        if (key.KeyChar is 'a' or 'A')
        {
            ToggleRun();
            return ScreenAction.Stay;
        }

        var options = BuildOptions();
        int index = key.KeyChar - '1';
        if (index >= 0 && index < options.Count && options[index].IsEnabled)
            return options[index].Execute();

        return ScreenAction.Stay;
    }

    private void ToggleRun()
    {
        IsRunning = !IsRunning;
        _timer.Restart();
    }

    /// <summary>
    /// Top-down ASCII sketch: X across, Y down at half scale (console cells
    /// are ~2:1 tall, so this renders roughly square). Sub-location edges
    /// are sketched; entities draw their initial; your pawn draws as '@'.
    /// </summary>
    private IEnumerable<string> BuildMap()
    {
        var locs = _controller.Locations!;
        var min = locs.All.Select(l => l.Min).Aggregate(Vector3.Min);
        var max = locs.All.Select(l => l.Max).Aggregate(Vector3.Max);

        int width  = Math.Clamp((int)Math.Ceiling(max.X - min.X), 1, 60);
        int height = Math.Clamp((int)Math.Ceiling((max.Y - min.Y) / 2), 1, 16);

        var grid = new char[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                grid[r, c] = '·';

        int Col(float x) => Math.Clamp((int)(x - min.X), 0, width - 1);
        int Row(float y) => Math.Clamp((int)((y - min.Y) / 2), 0, height - 1);

        // Sketch the edges of every sub-location (skip the world-sized one).
        float worldVolume = locs.All.Max(l => l.Volume);
        foreach (var loc in locs.All.Where(l => l.Volume < worldVolume))
        {
            int r0 = Row(loc.Min.Y), r1 = Row(loc.Max.Y);
            int c0 = Col(loc.Min.X), c1 = Col(loc.Max.X);
            for (int c = c0; c <= c1; c++) { grid[r0, c] = '─'; grid[r1, c] = '─'; }
            for (int r = r0; r <= r1; r++) { grid[r, c0] = '│'; grid[r, c1] = '│'; }
        }

        foreach (var e in _controller.ActiveEntities)
        {
            char glyph = e.ID == _controller.FavoredEntityId ? '@' : e.DisplayName[0];
            grid[Row(e.Position.Y), Col(e.Position.X)] = glyph;
        }

        for (int r = 0; r < height; r++)
        {
            var chars = new char[width];
            for (int c = 0; c < width; c++) chars[c] = grid[r, c];
            yield return new string(chars);
        }

        yield return string.Empty;
        yield return string.Join("   ", _controller.ActiveEntities.Select(e =>
        {
            char glyph = e.ID == _controller.FavoredEntityId ? '@' : e.DisplayName[0];
            return $"{glyph}={e.DisplayName} ({_controller.LocationTag(e)})";
        }));
    }

    private List<MenuOption> BuildOptions()
    {
        bool anyAlive = _controller.ActiveEntities.Count > 0;

        return
        [
            new MenuOption
            {
                Label = "Next Beat",
                Description = "Advance time until the next event fires  [Space]",
                Execute = () => { _controller.AdvanceToNextBeat(); return ScreenAction.Stay; }
            },
            new MenuOption
            {
                Label = IsRunning ? "Pause" : "Auto-Run",
                Description = $"Toggle real-time simulation ({EntityController.TicksPerSecond:0} ticks/sec)  [A]",
                Execute = () => { ToggleRun(); return ScreenAction.Stay; }
            },
            new MenuOption
            {
                Label = "Inspect Entity",
                IsEnabled = anyAlive,
                Execute = () => ScreenAction.Push(new EntityListScreen(_controller, EntityListMode.Inspect))
            },
            new MenuOption
            {
                Label = "Influence Entity",
                IsEnabled = anyAlive,
                Execute = () => ScreenAction.Push(new EntityListScreen(_controller, EntityListMode.Influence))
            }
        ];
    }
}
