using Game.Contracts;

namespace Game.Services.Effects;

/// <summary>
/// Signals that the item's uses should be decremented after this action.
/// The actual decrement is handled by ItemFactory/DataDrivenItem after Apply() returns.
/// </summary>
public class ConsumeItemEffect : IActionEffect
{
    public void Apply(Entity user, Entity target, Dictionary<string, float> parameters)
    {
        // Decrement is handled by DataDrivenItem.Apply() after all effects run.
    }
}
