using System.Numerics;

namespace Game.Contracts;

/// <summary>
/// The identity and state of a game actor. Contracts hold what an entity IS;
/// EntityService (Game.Services) knows HOW to operate on it; EntityController
/// (the game) decides WHEN. Only trivial self-mutators live here.
/// </summary>
public class Entity
{
    public string ID { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;

    public CombatStats Stats { get; private set; }
    public EntityNeeds Needs { get; private set; }
    public ActionModule Actions { get; private set; }
    public MindModule Mind { get; private set; }
    public HashSet<BehaviorModule> Behaviors { get; set; }
    public Dictionary<string, int> EntityRelationships { get; set; }
    public List<IItem> Inventory { get; set; }

    /// <summary>
    /// The entity as a describable object: (axis, valence) descriptors others
    /// can claim about and that seed its own boasts. Same shape as an item's
    /// descriptors — person, item, faction are all descriptor-bearing objects.
    /// </summary>
    public Dictionary<string, double> SelfDescriptors { get; set; }

    public Vector3 Position { get; set; } = Vector3.Zero;

    /// <summary>Physics footprint — the distinct space this entity occupies.
    /// Separation keeps two entities at least the sum of their footprints
    /// apart; interactions happen at reach, so nobody needs to overlap.</summary>
    public float PhysicsRadius { get; set; } = 0.6f;

    /// <summary>Interaction reach — how far (beyond footprints) this entity
    /// can act on a target. Also feeds decision weighting: distance weighs
    /// into whether a target is worth bothering with at all.</summary>
    public float InteractionRadius { get; set; } = 3f;

    /// <summary>Which band this entity belongs to, or null for a loner. The
    /// back-reference to <see cref="Party.MemberIds"/>; PartyService owns
    /// keeping the two ends agreeing.</summary>
    public string? PartyId { get; set; }

    public Influence? PendingInfluence { get; set; }

    /// <summary>
    /// What this entity is in the middle of, or null between commitments.
    /// </summary>
    /// <remarks>
    /// Only long actions ever leave one standing here: an instantaneous verb
    /// begins and settles inside a single beat, so it is never observed. The
    /// controller owns starting, advancing and settling it — see
    /// <see cref="Activity"/>.
    /// </remarks>
    public Activity? Current { get; set; }

    public bool IsAlive => Stats.Health.CurrentHealth > 0;

    public Entity()
    {
        Actions = new ActionModule();
        Mind = new MindModule();
        Behaviors = new HashSet<BehaviorModule>();
        SelfDescriptors = new Dictionary<string, double>();
        Stats = new CombatStats();
        Needs = new EntityNeeds();
        EntityRelationships = new Dictionary<string, int>();
        Inventory = new List<IItem>();
    }

    public double TakeDamage(double amount) => Stats.Health.TakeDamage(amount);
    public double GetCurrentHealth() => Stats.Health.CurrentHealth;
    public double GetMaxHealth() => Stats.Health.MaxHealth;
}
