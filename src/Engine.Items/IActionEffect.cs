namespace Engine.Items;

/// <summary>
/// A stateless, reusable unit of behavior triggered by an item action.
/// Implementations live in the consuming game (they know what TActor is);
/// they must be safe to register as a single shared instance.
/// </summary>
public interface IActionEffect<TActor>
{
    void Apply(TActor user, TActor target, Dictionary<string, float> parameters);
}
