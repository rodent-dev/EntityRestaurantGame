namespace EntityObjects;

/// <summary>
/// A player-applied nudge that boosts the weight of a specific action type for one tick.
/// Stored on Entity.PendingInfluence and consumed by EntityController.ProcessTick().
/// </summary>
public class Influence
{
    public string TargetActionName { get; init; } = string.Empty;
    public int Strength { get; init; }
    public string Source { get; init; } = "Player";
}
