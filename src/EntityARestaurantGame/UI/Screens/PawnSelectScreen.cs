using Game.Contracts;

namespace EntityARestaurantGame.UI.Screens;

/// <summary>
/// The intro: the player is a storyteller above the board and picks the pawn
/// to act through. Demo pawns serve as the pre-built choices for now; a real
/// pawn-creation flow can replace the list later without touching the world.
/// </summary>
public class PawnSelectScreen : IScreen
{
    private readonly EntityController _controller;
    private readonly Func<Entity> _spawnNobody;

    public PawnSelectScreen(EntityController controller, Func<Entity> spawnNobody)
    {
        _controller = controller;
        _spawnNobody = spawnNobody;
    }

    public string Title => "Choose Your Pawn";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar("You are a storyteller above the board. Choose the pawn to act through.");

        var lines = new List<string>
        {
            "Your pawn has its own mind — you nudge, it decides.",
            "Everyone else half-listens to a stranger's whispers.",
            string.Empty
        };
        foreach (var e in _controller.ActiveEntities)
        {
            lines.Add($"{e.DisplayName} — {e.EntityType}");
            lines.Add($"   Traits: {string.Join(", ", e.Behaviors.Select(b => b.Name))}");
            lines.Add($"   Carries: {string.Join(", ", e.Inventory.Select(i => i.ItemName))}");
            lines.Add(string.Empty);
        }

        renderer.DrawPanel(Title, lines);
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

    private List<MenuOption> BuildOptions() =>
        _controller.ActiveEntities.Select(e => new MenuOption
        {
            Label = $"Play as {e.DisplayName} the {e.EntityType}",
            Execute = () =>
            {
                _controller.FavoredEntityId = e.ID;
                _controller.Announce($"The narrator settles behind {e.DisplayName}'s eyes");
                return ScreenAction.Replace(new WorldScreen(_controller, _spawnNobody));
            }
        }).ToList();
}
