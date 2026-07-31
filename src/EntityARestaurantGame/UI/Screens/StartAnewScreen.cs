using Game.Contracts;

namespace EntityARestaurantGame.UI.Screens;

/// <summary>
/// Shown when the favored pawn dies. The world persists; only you reset.
/// Start anew as a fresh nobody (the canonical choice), or settle behind a
/// surviving pawn. No legacy, no residual identity either way.
/// </summary>
public class StartAnewScreen : IScreen
{
    private readonly EntityController _controller;
    private readonly Func<Entity> _spawnNobody;

    public StartAnewScreen(EntityController controller, Func<Entity> spawnNobody)
    {
        _controller = controller;
        _spawnNobody = spawnNobody;
    }

    public string Title => "Only You Reset";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        renderer.DrawPanel(Title,
        [
            "Your pawn is gone. The world does not pause to mourn —",
            "it was never revolving around you.",
            string.Empty,
            "Settle behind another pawn, or walk in as a stranger",
            "known by no one."
        ]);

        renderer.DrawOptions(BuildOptions());
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Quit");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Quit;

        var options = BuildOptions();
        int index = key.KeyChar - '1';
        if (index >= 0 && index < options.Count)
            return options[index].Execute();

        return ScreenAction.Stay;
    }

    private List<MenuOption> BuildOptions()
    {
        var options = _controller.ActiveEntities.Select(e => new MenuOption
        {
            Label = $"Settle behind {e.DisplayName} the {e.EntityType}",
            Execute = () =>
            {
                _controller.FavoredEntityId = e.ID;
                _controller.Announce($"The narrator settles behind {e.DisplayName}'s eyes");
                return ScreenAction.Pop;
            }
        }).ToList();

        options.Add(new MenuOption
        {
            Label = "Start anew — a stranger walks in",
            Description = "Spawn a fresh pawn with nothing to its name",
            Execute = () =>
            {
                var pawn = _spawnNobody();
                _controller.AddEntity(pawn);
                _controller.FavoredEntityId = pawn.ID;
                _controller.Announce($"{pawn.DisplayName} drifts in through the door");
                return ScreenAction.Pop;
            }
        });

        return options;
    }
}
