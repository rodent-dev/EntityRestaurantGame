using Game.Contracts;

namespace EntityARestaurantGame.UI.Screens;

public enum EntityListMode { Inspect, Influence }

public class EntityListScreen : IScreen
{
    private readonly EntityController _controller;
    private readonly EntityListMode _mode;

    public EntityListScreen(EntityController controller, EntityListMode mode)
    {
        _controller = controller;
        _mode = mode;
    }

    public string Title => _mode == EntityListMode.Inspect ? "Inspect — Select Entity" : "Influence — Select Entity";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        var lines = _controller.ActiveEntities.Select((e, i) =>
        {
            string dead = e.GetCurrentHealth() <= 0 ? " [dead]" : string.Empty;
            return $"[{i + 1}] {e.DisplayName}{dead}";
        });

        renderer.DrawPanel("Entities", lines);
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Back");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Pop;

        int index = key.KeyChar - '1';
        if (index < 0 || index >= _controller.ActiveEntities.Count) return ScreenAction.Stay;

        var entity = _controller.ActiveEntities[index];
        if (entity.GetCurrentHealth() <= 0) return ScreenAction.Stay;

        return _mode == EntityListMode.Inspect
            ? ScreenAction.Push(new EntityInspectScreen(entity, _controller))
            : ScreenAction.Push(new InfluenceScreen(entity, _controller));
    }
}
