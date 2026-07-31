using Game.Contracts;

namespace EntityARestaurantGame.UI.Screens;

public class InfluenceScreen : IScreen
{
    private readonly Entity _entity;
    private readonly EntityController _controller;
    private const int InfluenceStrength = EntityController.InfluenceStrength;

    public InfluenceScreen(Entity entity, EntityController controller)
    {
        _entity = entity;
        _controller = controller;
    }

    public string Title => $"Influence: {_entity.DisplayName}";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        var lines = new List<string>();
        bool stranger = _controller.FavoredEntityId != null && _entity.ID != _controller.FavoredEntityId;
        int effectivePct = (int)(_entity.Needs.MoodRatio * 100);
        int effectiveStr = (int)(InfluenceStrength * _entity.Needs.MoodRatio);
        if (stranger) effectiveStr /= EntityController.StrangerResistDivisor;
        lines.Add($"Nudge {_entity.DisplayName}'s next action toward one of the following:");
        lines.Add($"  Mood: {_entity.Needs.Mood}/{EntityNeeds.MaxMood}  —  influence effectiveness: {effectivePct}% (effective weight: +{effectiveStr})");
        if (stranger)
            lines.Add("  Not your pawn — strangers heed the narrator at half strength.");
        lines.Add(string.Empty);

        if (_entity.PendingInfluence is { } existing)
            lines.Add($"  ⚠  Pending influence already set: {existing.TargetActionName} (+{existing.Strength}) — selecting below replaces it.");

        renderer.DrawPanel(Title, lines);

        renderer.DrawOptions(BuildOptions());
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Back");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Pop;

        var options = BuildOptions();
        int index = key.KeyChar - '1';
        if (index >= 0 && index < options.Count && options[index].IsEnabled)
            return options[index].Execute();

        return ScreenAction.Stay;
    }

    private List<MenuOption> BuildOptions()
    {
        var actionNames = _entity.Actions.GetAllActions().Select(a => a.ActionName).ToList();
        return actionNames.Select(name => new MenuOption
        {
            Label = $"Nudge toward {name}  (+{InfluenceStrength} weight)",
            Execute = () =>
            {
                _entity.PendingInfluence = new Influence
                {
                    TargetActionName = name,
                    Strength = InfluenceStrength,
                    Source = "Player"
                };
                return ScreenAction.Push(new InfluenceResultScreen(_entity, name, InfluenceStrength));
            }
        }).ToList();
    }
}
