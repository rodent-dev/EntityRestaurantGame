# Module 03 — Action Resolution System
**Parent:** [Overview](./00_overview.md)
**Status:** Draft v0.1

---

## Responsibility

The Action Resolution System defines what an action *is*, how it is structured, and what happens when it executes. It is the bridge between the Behavior Weighting Engine's selection and the actual change of state in the world.

---

## Core Concept: Actions Always Have a Target and an Item

Every action in the simulation has two mandatory structural elements:

1. **A target** — the entity the action affects. This can be the actor themselves (self-targeted).
2. **An item** — the vehicle through which the action is expressed. Items are not optional props — they define what kind of action is possible and what its effects are.

This rule keeps the action model consistent and queryable. The question "what can this entity do right now?" reduces to "what items do they have, and what actions do those items enable?"

---

## ActionDefinition

An `ActionDefinition` is a template. It describes what an action does, not a specific instance of it being performed.

```csharp
public class ActionDefinition
{
    public string Id { get; set; }                      // Unique string key, e.g., "action.attack.melee"
    public string DisplayName { get; set; }
    public List<BehaviorTag> BehaviorTags { get; set; } // Links to weighting engine
    public TargetingRule Targeting { get; set; }        // Who/what can be targeted
    public string RequiredItemTag { get; set; }         // Item tag required to perform this action
    public List<IActionEffect> Effects { get; set; }    // What the action does on resolution
    public ActionCost Cost { get; set; }                // Time/resource cost
}
```

---

## Item

Items are the required attachment for any action. They are not just consumables — they carry semantic identity that enables specific action types.

```csharp
public class Item
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Tag { get; set; }              // e.g., "item.weapon.melee", "item.trophy", "item.bandage"
    public List<IItemBehavior> Behaviors { get; set; }  // Attachable behaviors (see below)
    public Dictionary<string, float> Properties { get; set; }  // Flexible numeric properties
}
```

### Item Behaviors (Attachable)

Items have their own modular behaviors that modify how they interact with actions and the world. These are not entity behaviors — they are item-level traits.

```csharp
public interface IItemBehavior
{
    void OnActionUsed(Item item, ActionContext context);
}
```

**Key built-in item behaviors:**

| Behavior | Description |
|----------|-------------|
| `ConsumableOnUse` | Item is removed from inventory after the action resolves |
| `DegradingDurability` | Item's effectiveness decreases with each use |
| `AccomplishmentMarker` | Item represents a past achievement; increases credibility of social actions |
| `WeaponProperties` | Carries damage range, damage type, and reach multiplier |
| `HealingProperties` | Carries heal amount; applicable to healing actions |

**The `AccomplishmentMarker` behavior** deserves special note: a trophy, a captured banner, or proof of a deed is an item that carries social weight. Attaching it to a `Speak` or `Negotiate` action increases the weight of a favorable relationship outcome, making unlikely alliances achievable through demonstrated credibility.

---

## TargetingRule

Defines who or what can be targeted by an action.

```csharp
public class TargetingRule
{
    public TargetType Type { get; set; }   // Self, Ally, Enemy, Any, Area
    public float MaxRange { get; set; }    // 0 = self-only; uses entity's position + reach
    public Predicate<Entity> Filter { get; set; } // Optional custom filter (e.g., "only wounded")
}

public enum TargetType
{
    Self,
    Ally,
    Enemy,
    AnyEntity,
    GroundPosition,
    Area
}
```

---

## ActionEffect System

Effects are the atomic units of what an action actually does. An action can carry multiple effects that all resolve when the action executes.

```csharp
public interface IActionEffect
{
    void Apply(ActionContext context);
}
```

**Context passed to every effect:**

```csharp
public class ActionContext
{
    public Entity Actor { get; set; }
    public Entity Target { get; set; }       // Null for positional/area actions
    public Item UsedItem { get; set; }
    public EncounterContext Encounter { get; set; }
    public SimulationState World { get; set; }
}
```

### Built-in Effects

| Effect Class | Description |
|--------------|-------------|
| `DamageEffect` | Applies damage to target's `health` stat, considering item weapon properties |
| `HealEffect` | Restores target's `health` stat |
| `RelationshipEffect` | Modifies actor's affinity toward target (or vice versa) by a defined delta |
| `StatModifyEffect` | Temporarily or permanently modifies a stat key on target |
| `MoveEffect` | Repositions an entity to a new location |
| `ConsumeItemEffect` | Triggers `ConsumableOnUse` behavior on the used item |
| `ApplyStatusEffect` | Attaches a status (e.g., `Stunned`, `Frightened`) to the target entity |

Effects are composable. A `Speak` action carrying an `AccomplishmentMarker` item might resolve with:
- `RelationshipEffect(+0.2)` — mild affinity gain for the attempt
- Conditional `RelationshipEffect(+0.5)` — additional gain if the item has `AccomplishmentMarker`

---

## ActionCost

Actions consume resources. Cost is checked before an action is committed.

```csharp
public class ActionCost
{
    public float TimeCost { get; set; }       // Simulation time units consumed
    public float StaminaCost { get; set; }    // Deducted from actor's stamina stat
    public bool RequiresItem { get; set; }    // If true, item must be present and valid
}
```

The Entity Control System (Module 04) uses `TimeCost` to schedule when an entity is eligible for its next action.

---

## Action Resolution Flow

```
1. BehaviorWeightingEngine selects ActionDefinition for actor
2. ActionResolutionSystem.Resolve(actor, action, context) called
3. Validate:
   a. Actor has required item in inventory (or equipped)
   b. Target is valid per TargetingRule
   c. Actor can afford ActionCost
4. Deduct ActionCost from actor
5. For each IActionEffect in action.Effects:
       effect.Apply(context)
6. For each IItemBehavior on used item:
       behavior.OnActionUsed(item, context)
7. Raise ActionResolvedEvent (for UI, logging, other systems to observe)
8. EntityControlSystem schedules actor's next decision based on TimeCost
```

---

## Action Examples

### Melee Attack
```
Id: "action.attack.melee"
BehaviorTags: [Aggressive]
Targeting: { Type: Enemy, MaxRange: actor.Physique.ReachRange }
RequiredItemTag: "item.weapon.melee"
Effects:
    - DamageEffect (damage from item.Properties["damage_min/max"])
    - ConsumeItemEffect (if item has ConsumableOnUse — e.g., a thrown knife)
Cost: { TimeCost: 1.0, StaminaCost: 0.1 }
```

### Speak / Negotiate
```
Id: "action.social.speak"
BehaviorTags: [Social]
Targeting: { Type: AnyEntity, MaxRange: 5.0 }
RequiredItemTag: "item.social.*"   // Any social item — words, trophies, gifts
Effects:
    - RelationshipEffect(delta: +0.1)
    - Conditional RelationshipEffect(delta: +0.4, condition: item.HasBehavior<AccomplishmentMarker>)
Cost: { TimeCost: 0.8, StaminaCost: 0.0 }
```

### Use Bandage (Self-Heal)
```
Id: "action.heal.self"
BehaviorTags: [Cautious]
Targeting: { Type: Self }
RequiredItemTag: "item.consumable.bandage"
Effects:
    - HealEffect (heal amount from item.Properties["heal_amount"])
    - ConsumeItemEffect
Cost: { TimeCost: 1.2, StaminaCost: 0.0 }
```

---

## Design Notes

- **Items are not optional flavor.** An action without an item cannot be constructed — the system enforces this. This creates natural constraints: an entity without a weapon cannot attack, and an entity without social items has limited diplomacy options.
- **Effects are the extension point.** New action types are created by writing new `IActionEffect` implementations and composing them, not by modifying the action class or adding switch statements.
- **The action template (ActionDefinition) is distinct from the action event.** What happened (actor, target, item, outcome) is captured in `ActionContext` and broadcast via `ActionResolvedEvent` for observers like the UI, the relationship system, and a replay log.
- **Items as accomplishments are a design hook, not a special case.** An `AccomplishmentMarker` is just another `IItemBehavior`. The system does not know or care that a trophy is special — it just processes its behavior like any other item behavior. The *weight* it carries comes from how `RelationshipEffect` uses it.
