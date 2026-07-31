namespace Game.Contracts;

/// <summary>
/// A player-applied nudge that boosts the weight of a specific action type
/// until the entity's next executed action. Stored on Entity.PendingInfluence
/// and consumed by the decision loop.
/// </summary>
public class Influence
{
    public string TargetActionName { get; init; } = string.Empty;

    /// <summary>
    /// Optional: WHO to do it to. Null leaves the choice to the sim (the
    /// original untargeted whisper). Set, and the nudge only lands on
    /// candidates aimed at this entity — which is what makes "interact with
    /// THAT one" expressible at all. Reach is not this concern's problem: a
    /// far target simply turns the beat into an Approach, and the intent
    /// survives the walk.
    /// </summary>
    public string? TargetEntityId { get; init; }

    public int Strength { get; init; }
    public string Source { get; init; } = "Player";
}
