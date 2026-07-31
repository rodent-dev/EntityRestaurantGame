namespace EntityObjects;

/// <summary>
/// Tracks an entity's Hunger and Mood — the two mutable needs that drive
/// Status-based weight adjustments.  Hunger decays each tick; Mood shifts
/// in response to in-game events (attacks, gifts, eating, etc.).
/// </summary>
public class EntityNeeds
{
    public const int MaxHunger = 100;
    public const int MaxMood = 100;
    public const int HungerDecayPerTick = 2;

    public int Hunger { get; private set; } = MaxHunger;
    public int Mood { get; private set; } = 75;

    public double HungerRatio => (double)Hunger / MaxHunger;
    public double MoodRatio => (double)Mood / MaxMood;

    public void TickHunger(int amount = HungerDecayPerTick)
    {
        Hunger = Math.Max(0, Hunger - amount);
    }

    public void AdjustMood(int delta)
    {
        Mood = Math.Clamp(Mood + delta, 0, MaxMood);
    }

    public void Feed(int amount)
    {
        Hunger = Math.Clamp(Hunger + amount, 0, MaxHunger);
    }
}
