namespace EntityObjects.Effects;

/// <summary>
/// A stateless, reusable unit of behavior triggered by an item action.
/// Implementations must be safe to register as a single shared instance.
/// </summary>
public interface IActionEffect
{
    void Apply(Entity user, Entity target, Dictionary<string, float> parameters);
}
