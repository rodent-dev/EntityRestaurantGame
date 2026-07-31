using EntityObjects;

namespace EntityARestaurantGame.UI.Screens;

public class InfluenceResultScreen : IScreen
{
    private readonly Entity _entity;
    private readonly string _actionName;
    private readonly int _strength;

    public InfluenceResultScreen(Entity entity, string actionName, int strength)
    {
        _entity = entity;
        _actionName = actionName;
        _strength = strength;
    }

    public string Title => "Influence Applied";

    public void Render(ConsoleRenderer renderer)
    {
        renderer.DrawStatusBar(Title);

        bool hasAction = _entity.Actions.HasAction(_actionName);
        bool canActorUse = hasAction &&
            _entity.Actions.GetAction(_actionName)!.CanActorUse(_entity);

        string status;
        string reason;

        if (!hasAction)
        {
            status = "Rejected";
            reason = $"{_entity.DisplayName} does not have the '{_actionName}' action registered.";
        }
        else if (!canActorUse)
        {
            status = "Partially accepted";
            reason = $"{_entity.DisplayName} cannot currently use '{_actionName}' (missing required item). Influence queued but may have no effect.";
        }
        else
        {
            status = "Accepted";
            reason = $"'{_actionName}' weight boosted by +{_strength} for {_entity.DisplayName}'s next turn.";
        }

        renderer.DrawPanel(Title,
        [
            $"Target:   {_entity.DisplayName}",
            $"Nudge:    {_actionName}  (+{_strength} weight)",
            $"Status:   {status}",
            string.Empty,
            reason
        ]);

        renderer.WriteLine();
        renderer.WriteLine("  [Enter / any key] Return to World");
    }

    public ScreenAction HandleInput(ConsoleKeyInfo key) =>
        ScreenAction.Pop;
}
