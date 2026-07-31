using EntityObjects;

namespace EntityARestaurantGame.UI.Screens;

public class EntityInspectScreen : IScreen
{
    private readonly Entity _entity;
    private readonly EntityController _controller;

    public EntityInspectScreen(Entity entity, EntityController controller)
    {
        _entity = entity;
        _controller = controller;
    }

    public string Title => $"Inspect: {_entity.DisplayName}";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar($"Tick: {_controller.CurrentTick}  |  {Title}");

        var lines = new List<string>();

        lines.Add($"Type: {_entity.EntityType}   HP: {_entity.GetCurrentHealth():F0} / {_entity.GetMaxHealth():F0}");
        lines.Add(string.Empty);

        lines.Add("── Status ──");
        lines.Add($"  Hunger:  {_entity.Needs.Hunger}/{EntityObjects.EntityNeeds.MaxHunger}  ({HungerLabel(_entity.Needs.HungerRatio)})");
        lines.Add($"  Mood:    {_entity.Needs.Mood}/{EntityObjects.EntityNeeds.MaxMood}  ({MoodLabel(_entity.Needs.MoodRatio)})");
        int influencePct = (int)(_entity.Needs.MoodRatio * 100);
        lines.Add($"  Influence effectiveness: {influencePct}%");
        lines.Add(string.Empty);

        lines.Add("── Behaviors ──");
        if (_entity.Behaviors.Count == 0)
            lines.Add("  (none)");
        else
            foreach (var b in _entity.Behaviors)
                lines.Add($"  {b.Name,-14} [{string.Join(", ", b.Keywords)}]  affect={b.Affect:+#;-#;0}");

        lines.Add(string.Empty);
        lines.Add("── Relationships ──");
        if (_entity.EntityRelationships.Count == 0)
            lines.Add("  (none)");
        else
            foreach (var (id, score) in _entity.EntityRelationships)
            {
                var target = _controller.ActiveEntities.FirstOrDefault(e => e.ID == id);
                string name = target?.DisplayName ?? id;
                lines.Add($"  {name,-14} affinity={score:+#;-#;0}");
            }

        lines.Add(string.Empty);
        lines.Add("── Recent Decisions ──");
        var history = _controller.GetDecisionHistory(_entity.ID);
        if (history.Count == 0)
            lines.Add("  No decisions yet");
        else
            foreach (var d in history)
                lines.Add($"  {d.ActionName} → {d.TargetName}  (driven by {d.PrimaryFactor})");

        renderer.DrawPanel(Title, lines);

        renderer.DrawOptions(
        [
            new MenuOption
            {
                Label = "Influence This Entity",
                Execute = () => ScreenAction.Replace(new InfluenceScreen(_entity, _controller))
            }
        ]);
        renderer.WriteLine();
        renderer.WriteLine("  [Esc] Back");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) return ScreenAction.Pop;
        if (key.KeyChar == '1') return ScreenAction.Replace(new InfluenceScreen(_entity, _controller));
        return ScreenAction.Stay;
    }

    private static string HungerLabel(double ratio) => ratio switch
    {
        >= 0.75 => "Stuffed",     // exceeded — complacent
        >= 0.50 => "Satisfied",   // sweet spot
        >= 0.25 => "Hungry",      // deficit
        _       => "Starving"     // critical
    };

    private static string MoodLabel(double ratio) => ratio switch
    {
        >= 0.75 => "Giddy",       // exceeded — overly social
        >= 0.50 => "Content",     // sweet spot
        >= 0.25 => "Uneasy",      // deficit
        _       => "Miserable"    // critical
    };
}
