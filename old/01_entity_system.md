# Module 01 — Entity System
**Parent:** [Overview](./00_overview.md)
**Status:** Draft v0.1

---

## Responsibility

The Entity is the root domain object of the simulation. It represents any character — player-controlled (PC) or non-player-controlled (NPC). The Entity itself is intentionally thin; it owns an identity and a set of component slots, but no hardcoded stats or behaviors. Everything is attached.

---

## Core Concepts

### Entity

An Entity is a named, identifiable participant in the simulation. It serves as the aggregation root for all attached components.

```csharp
public class Entity
{
    public Guid Id { get; }
    public string Name { get; set; }
    public EntityTag Tags { get; set; }              // e.g., PC, NPC, Faction member

    public Physique Physique { get; set; }
    public StatBlock Stats { get; set; }
    public BehaviorProfile BehaviorProfile { get; set; }
    public ActionRegistry ActionRegistry { get; set; }
    public RelationshipMap Relationships { get; set; }
    public Inventory Inventory { get; set; }
}
```

---

### Physique (Attachment)

The Physique defines the physical shape and capability envelope of an entity. It is modular — a creature, a humanoid, and a vehicle could each have completely different Physiques without the Entity class caring.

```csharp
public class Physique
{
    public float Size { get; set; }            // Affects hit chance, cover, detection radius
    public float MoveSpeed { get; set; }
    public float ReachRange { get; set; }      // Melee/interaction range
    public List<BodyPart> BodyParts { get; set; }  // Limbs, slots for equipment
    public PhysiqueTag Tags { get; set; }      // Humanoid, Quadruped, Mechanical, etc.
}
```

`BodyParts` are used both for damage modeling and as equipment slots. For example, a humanoid has two hand slots, each of which can hold an Item.

---

### StatBlock (Attachment)

Stats represent an entity's numeric aptitudes. Stats are referenced by behaviors and actions — they are not a fixed list. The simulation references stats by string key to remain extensible.

```csharp
public class StatBlock
{
    private readonly Dictionary<string, float> _stats = new();

    public float Get(string key, float defaultValue = 0f) =>
        _stats.TryGetValue(key, out var v) ? v : defaultValue;

    public void Set(string key, float value) => _stats[key] = value;

    public void Modify(string key, float delta) =>
        _stats[key] = Get(key) + delta;
}
```

**Conventional stat keys** (not hardcoded — just established convention):

| Key | Description |
|-----|-------------|
| `strength` | Physical power — affects melee damage, carry weight |
| `agility` | Speed and evasion contribution |
| `perception` | Detection range, accuracy |
| `resolve` | Resistance to morale effects |
| `social` | Effectiveness of speech and relationship actions |
| `health` | Current hit points |
| `health_max` | Maximum hit points |

---

### BehaviorProfile (Attachment)

This is the personality of the entity — a set of weighted behavioral dispositions that seed the weighting engine. See [Module 02](./02_behavior_weighting.md) for full details.

```csharp
public class BehaviorProfile
{
    // Each entry represents a disposition and its base weight contribution
    public List<BehaviorDisposition> Dispositions { get; set; } = new();
}

public class BehaviorDisposition
{
    public BehaviorTag Tag { get; set; }   // e.g., Aggressive, Cautious, Social, Cowardly
    public float Weight { get; set; }      // Base contribution to matching action weights
}
```

A disposition does not directly select actions — it raises or lowers the probability that the weighting engine favors actions tagged with matching behavior categories.

---

### ActionRegistry (Attachment)

The ActionRegistry holds all actions available to a specific entity. Actions are not globally defined — an entity can only perform actions it has registered. This is intentional: a mute creature cannot Speak; an unarmed entity cannot Shoot.

```csharp
public class ActionRegistry
{
    private readonly List<ActionDefinition> _actions = new();

    public void Register(ActionDefinition action) => _actions.Add(action);
    public void Unregister(string actionId) => _actions.RemoveAll(a => a.Id == actionId);
    public IReadOnlyList<ActionDefinition> GetAll() => _actions.AsReadOnly();
}
```

See [Module 03](./03_action_resolution.md) for `ActionDefinition`.

---

### RelationshipMap (Attachment)

Tracks this entity's disposition toward other specific entities. These values feed into the weighting engine as environmental/relational modifiers.

```csharp
public class RelationshipMap
{
    // Key: target entity Id. Value: affinity score, typically in range [-1.0, 1.0]
    private readonly Dictionary<Guid, float> _affinities = new();

    public float GetAffinity(Guid targetId, float defaultValue = 0f) =>
        _affinities.TryGetValue(targetId, out var v) ? v : defaultValue;

    public void SetAffinity(Guid targetId, float value) =>
        _affinities[targetId] = Math.Clamp(value, -1f, 1f);

    public void ModifyAffinity(Guid targetId, float delta) =>
        SetAffinity(targetId, GetAffinity(targetId) + delta);
}
```

---

### Inventory (Attachment)

An entity's carried items. Items in the inventory are candidates to be attached to actions. Items must be present in the inventory (or equipped) for the corresponding action to be available to the weighting engine.

```csharp
public class Inventory
{
    private readonly List<Item> _items = new();

    public void Add(Item item) => _items.Add(item);
    public bool Remove(Item item) => _items.Remove(item);
    public IReadOnlyList<Item> GetAll() => _items.AsReadOnly();
    public bool Has(string itemTag) => _items.Any(i => i.Tags.HasFlag(itemTag));
}
```

See [Module 03](./03_action_resolution.md) for `Item`.

---

## Entity Lifecycle

```
Create Entity
    └─ Attach Physique
    └─ Attach StatBlock (initial values)
    └─ Attach BehaviorProfile (dispositions)
    └─ Attach ActionRegistry (available actions)
    └─ Attach RelationshipMap (seeded or empty)
    └─ Attach Inventory (starting items)

Register with EntityControlSystem (see Module 04)

Per Tick:
    └─ EntityControlSystem determines if this entity is a candidate for action
    └─ BehaviorWeightingEngine scores available actions (Module 02)
    └─ Top-scored action is selected (with jitter)
    └─ ActionResolutionSystem executes (Module 03)
    └─ State changes applied back to entities
```

---

## Design Notes

- **No base class inheritance for behavior.** Entities do not subclass into `AggressiveEntity` or `SocialEntity`. Personality lives entirely in the `BehaviorProfile`.
- **Components can be hot-swapped.** An entity that loses a limb can have its `Physique` updated; an entity that learns a skill can have a new `ActionDefinition` registered. No special re-initialization is needed.
- **EntityTag enum** is a flags enum, allowing entities to carry multiple categorical tags simultaneously (e.g., `PC | FactionMember | Wounded`).
