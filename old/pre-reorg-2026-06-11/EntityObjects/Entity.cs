using System.Numerics;
using EntityObjects.Interfaces;

namespace EntityObjects;

public class Entity
{
    public string ID { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;

    public CombatStats Stats { get; private set; }
    public EntityPhysique Physique { get; private set; }
    public EntityNeeds Needs { get; private set; }
    public ActionModule Actions { get; private set; }
    public HashSet<BehaviorModule> Behaviors { get; set; }
    public Dictionary<string, int> EntityRelationships { get; set; }
    public List<IItem> Inventory { get; set; }

    public Vector3 Position { get; set; } = Vector3.Zero;
    public Influence? PendingInfluence { get; set; }

    public bool IsAlive => Stats.Health.CurrentHealth > 0;

    public Entity()
    {
        Actions = new ActionModule();
        Behaviors = new HashSet<BehaviorModule>();
        Stats = new CombatStats();
        Physique = new EntityPhysique();
        Needs = new EntityNeeds();
        EntityRelationships = new Dictionary<string, int>();
        Inventory = new List<IItem>();
    }

    public ActionModule GetActionModule() => Actions;
    public double TakeDamage(double amount) => Stats.Health.TakeDamage(amount);
    public double GetCurrentHealth() => Stats.Health.CurrentHealth;
    public double GetMaxHealth() => Stats.Health.MaxHealth;

    public void ApplyStatusWeights(List<ActionCandidate> candidates) =>
        StatusWeightCalculator.Apply(this, candidates);

    public List<ActionCandidate> BuildCandidates(List<Entity> activeEntities)
    {
        var candidates = new List<ActionCandidate>();

        foreach (var action in Actions.GetAllActions())
        {
            if (!action.CanActorUse(this))
                continue;

            foreach (var target in activeEntities)
            {
                if (!action.CanTarget(this, target))
                    continue;

                var candidate = new ActionCandidate(action, target);
                if (action.BaseWeight != 0)
                    candidate.Weights.Add(new ActionWeight { Weight = action.BaseWeight, Source = ActionWeightSource.Base });
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    public void ApplyBehaviorWeights(List<ActionCandidate> candidates)
    {
        var relationshipWeights = candidates
            .Select(c => c.Target.ID)
            .Distinct()
            .Where(id => EntityRelationships.ContainsKey(id))
            .ToDictionary(id => id, id => EntityRelationships[id]);

        foreach (var candidate in candidates)
        {
            // Sum keyword-matched behavior weights.
            // Each behavior contributes: affect * scalar * matchCount
            int behaviorWeight = 0;
            foreach (var behavior in Behaviors)
            {
                int matches = behavior.MatchCount(candidate.Action.Keywords);
                if (matches > 0)
                    behaviorWeight += behavior.Affect * behavior.Scalar * matches;
            }

            if (behaviorWeight != 0)
                candidate.Weights.Add(new ActionWeight { Weight = behaviorWeight, Source = ActionWeightSource.Behaviors });

            if (candidate.Target != null && relationshipWeights.TryGetValue(candidate.Target.ID, out var rWeight))
                candidate.Weights.Add(new ActionWeight { Weight = rWeight, Source = ActionWeightSource.Relationship });
        }
    }
}

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

    public CombatStats(int str, int agi, int intel, int con, int wis, int cha)
    {
        Strength = str; Agility = agi; Intelligence = intel;
        Constitution = con; Wisdom = wis; Charisma = cha;
        Health = new EntityHealth(con);
    }

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

public class EntityHealth
{
    public double MaxHealth { get; private set; }
    public double CurrentHealth { get; private set; }

    public EntityHealth(int constitution = 10, double weight = 100, double height = 6)
    {
        MaxHealth = (constitution * 10) + (weight * 0.2) - (height * 0.1);
        CurrentHealth = MaxHealth;
    }

    public double TakeDamage(double amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        return CurrentHealth;
    }

    public double Heal(double amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        return CurrentHealth;
    }

    public void UpdateFromStats(int constitution, double weight, double height)
    {
        double ratio = CurrentHealth / MaxHealth;
        MaxHealth = (constitution * 10) + (weight * 0.2) - (height * 0.1);
        CurrentHealth = MaxHealth * ratio;
    }
}

public class EntityPhysique
{
    public double Weight { get; set; }
    public double Height { get; set; }
}
