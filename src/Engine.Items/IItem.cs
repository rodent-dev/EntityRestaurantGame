namespace Engine.Items;

/// <summary>
/// A game-agnostic item contract: identity, durability, and the actions it
/// enables. TActor is the consuming game's actor type; the engine never
/// inspects it — it only routes it through to registered effects.
/// </summary>
public interface IItem<TActor>
{
    string ItemId { get; }
    string ItemName { get; }
    string Description { get; }
    float Weight { get; }

    /// <summary>Remaining uses; -1 means infinite, 0 means depleted.</summary>
    int UsesRemaining { get; set; }

    /// <summary>
    /// Axis-keyed valence descriptors ("what this item is"), e.g. provenance
    /// or quality. The engine never interprets them — consuming games read
    /// them for salience/valuation. Empty by default.
    /// </summary>
    IReadOnlyDictionary<string, double> Descriptors => EmptyDescriptors;

    protected static IReadOnlyDictionary<string, double> EmptyDescriptors { get; } =
        new Dictionary<string, double>();

    bool CanEnableAction(string actionName, TActor user, TActor target);
    void Apply(string actionName, TActor user, TActor target);
}
