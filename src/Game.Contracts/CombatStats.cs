namespace Game.Contracts;

public class CombatStats
{
    const int DEFAULT_STAT = 10;
    const double NEGATION_THRESHOLD = 0.33;
    private static readonly Random _rng = new();

    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Intelligence { get; set; }
    public int Constitution { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
    public EntityHealth Health { get; set; }

    public CombatStats(bool isRandom = false)
    {
        Strength = RollStat(isRandom);
        Agility = RollStat(isRandom);
        Intelligence = RollStat(isRandom);
        Constitution = RollStat(isRandom);
        Wisdom = RollStat(isRandom);
        Charisma = RollStat(isRandom);
        Health = new EntityHealth(Constitution);
    }

    private static int RollStat(bool isRandom)
    {
        if (!isRandom) return DEFAULT_STAT;
        bool negative = _rng.NextDouble() <= NEGATION_THRESHOLD;
        return DEFAULT_STAT + (negative ? -_rng.Next(0, 10) : _rng.Next(0, 10));
    }
}
