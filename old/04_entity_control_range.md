# Module 04 — Entity Control & Range System
**Parent:** [Overview](./00_overview.md)
**Status:** Draft v0.1

---

## Responsibility

The Entity Control & Range System determines *which entities are considered* for action at any given simulation tick, and *how likely they are to be involved* based on their proximity to an encounter. It is the scheduler and relevance filter of the simulation — it does not decide what entities do, but it decides whether they get a turn and with what contextual awareness.

---

## Core Concepts

### Encounter

An `Encounter` is any situation where entities may take action relative to one another. Encounters have a spatial location and a radius of influence. Entities within or near this radius are evaluated for participation.

```csharp
public class Encounter
{
    public Guid Id { get; set; }
    public Vector2 Position { get; set; }
    public float CoreRadius { get; set; }        // Active zone — entities here are highly likely to act
    public float AwarenessRadius { get; set; }   // Outer zone — entities here may be drawn in
    public List<Entity> ActiveParticipants { get; set; }
    public EncounterContext Context { get; set; }
}
```

---

## Participation Likelihood

Entities are not simply "in" or "out" of an encounter. Their likelihood of being considered for action decays with distance from the encounter's core.

```csharp
public float ComputeParticipationWeight(Entity entity, Encounter encounter)
{
    float distance = Vector2.Distance(entity.Position, encounter.Position);

    if (distance <= encounter.CoreRadius)
        return 1.0f;  // Fully in — always considered

    if (distance > encounter.AwarenessRadius)
        return 0.0f;  // Out of awareness entirely — not considered

    // Linear falloff between core and awareness radius
    float range = encounter.AwarenessRadius - encounter.CoreRadius;
    float excess = distance - encounter.CoreRadius;
    float falloff = 1f - (excess / range);

    // Apply perception modifier — perceptive entities notice more at range
    float perception = entity.Stats.Get("perception", 1f);
    return Math.Clamp(falloff * perception, MinParticipationWeight, 1.0f);
}
```

The `MinParticipationWeight` constant (e.g., `0.02f`) ensures that even distant entities are never at absolute zero — a very lucky roll can always pull in a bystander. This is the "not 0, but unlikely" design requirement.

---

## The Control Loop

The `EntityControlSystem` is the simulation's heartbeat. It runs on a tick and decides, for each registered entity, whether it gets to act this tick and in what context.

```csharp
public class EntityControlSystem
{
    private readonly List<Entity> _entities = new();
    private readonly List<Encounter> _activeEncounters = new();
    private readonly BehaviorWeightingEngine _weightingEngine;
    private readonly ActionResolutionSystem _resolutionSystem;
    private readonly Random _rng;

    public void Tick(float deltaTime)
    {
        foreach (var entity in _entities)
        {
            // Cooldown tracking — entity not eligible until its TimeCost has expired
            entity.ActionCooldown -= deltaTime;
            if (entity.ActionCooldown > 0f) continue;

            // Find the most relevant encounter for this entity
            var (encounter, participationWeight) = GetMostRelevantEncounter(entity);

            if (encounter == null) continue;

            // Roll against participation weight — this is where range-based likelihood lives
            if (_rng.NextSingle() > participationWeight) continue;

            // Build the action candidate list valid for this encounter context
            var candidates = BuildCandidateActions(entity, encounter);
            if (candidates.Count == 0) continue;

            // Delegate to weighting engine for selection
            var action = _weightingEngine.SelectAction(
                entity,
                candidates,
                encounter.Context,
                encounter.Context.ActiveModifiers);

            if (action == null) continue;

            // Resolve the selected action
            _resolutionSystem.Resolve(entity, action, encounter.Context);

            // Schedule next action opportunity
            entity.ActionCooldown = action.Cost.TimeCost;
        }
    }
}
```

---

## Candidate Action Building

Not all of an entity's registered actions are valid in every context. Candidates are filtered based on:

1. **Item availability** — the required item must be in the entity's inventory or equipped.
2. **Valid targets exist** — at least one entity satisfies the action's `TargetingRule` within the encounter.
3. **Range** — the targeting rule's `MaxRange` is checked against the entity's current position relative to potential targets.

```csharp
private List<ActionDefinition> BuildCandidateActions(Entity actor, Encounter encounter)
{
    var candidates = new List<ActionDefinition>();

    foreach (var action in actor.ActionRegistry.GetAll())
    {
        // Item check
        if (!actor.Inventory.Has(action.RequiredItemTag)) continue;

        // Target availability check
        bool hasValidTarget = encounter.ActiveParticipants.Any(target =>
            target != actor &&
            IsValidTarget(actor, target, action.Targeting));

        // Self-targeted actions always have a valid target
        if (action.Targeting.Type == TargetType.Self) hasValidTarget = true;

        if (hasValidTarget) candidates.Add(action);
    }

    return candidates;
}
```

---

## Encounter Relevance Ranking

When an entity is within range of multiple encounters, the system selects the most relevant one to resolve against. Relevance is scored by a combination of distance and encounter intensity.

```csharp
private (Encounter encounter, float weight) GetMostRelevantEncounter(Entity entity)
{
    Encounter best = null;
    float bestWeight = 0f;

    foreach (var encounter in _activeEncounters)
    {
        float weight = ComputeParticipationWeight(entity, encounter);
        float intensity = encounter.Context.IntensityScore; // How "hot" the encounter is

        float relevance = weight * (1f + intensity * IntensityInfluenceFactor);

        if (relevance > bestWeight)
        {
            bestWeight = relevance;
            best = encounter;
        }
    }

    return (best, bestWeight);
}
```

`IntensityScore` is a dynamic value on the `EncounterContext` that rises as more actions resolve, more entities participate, and health totals change. This means that as a fight escalates, it pulls in nearby bystanders more aggressively.

---

## Entity Registration & Tracking

```csharp
public void Register(Entity entity)
{
    _entities.Add(entity);
    entity.ActionCooldown = 0f;  // Eligible immediately on first tick
}

public void Unregister(Entity entity)
{
    _entities.Remove(entity);
}

public void RegisterEncounter(Encounter encounter)
{
    _activeEncounters.Add(encounter);
}

public void CloseEncounter(Guid encounterId)
{
    _activeEncounters.RemoveAll(e => e.Id == encounterId);
}
```

---

## Position & Spatial Model

The system uses a simple 2D positional model. Entities have a `Vector2 Position`. This is intentionally minimal — the spatial system can be swapped for a tile-based, graph-based, or zone-based model later without changing the participation weight formula's interface.

```csharp
// Minimal position attachment on Entity
public Vector2 Position { get; set; }
public float ActionCooldown { get; set; }
```

---

## Encounter Lifecycle

```
Encounter Created
    └─ CoreRadius and AwarenessRadius set
    └─ Initial participants added (e.g., attacker + defender who triggered it)
    └─ Registered with EntityControlSystem

Each Tick:
    └─ EntityControlSystem evaluates all registered entities
    └─ Entities within AwarenessRadius are rolled against participation weight
    └─ Passing entities have candidates built and actions selected
    └─ IntensityScore updated based on resolved actions

Encounter Ends When:
    └─ All combatants on one side are incapacitated / fled / resolved
    └─ Or explicit close condition met (e.g., player disbands encounter)

EntityControlSystem.CloseEncounter(id) called
    └─ Participants' cooldowns expire naturally
    └─ Entities return to ambient / idle state
```

---

## Design Notes

- **Participation weight is not a hard gate.** Every entity has some non-zero chance of involvement once they're within the awareness radius. This enables emergent events like a passerby suddenly joining a fight — rare, but possible.
- **The tick loop does not guarantee fairness.** Entities with lower `TimeCost` actions will act more frequently than slow entities. This is intended — a fast, agile character taking many small actions vs. a slow heavy-hitter taking few large ones is a meaningful design dimension.
- **Encounter intensity as a gravity well.** The `IntensityScore` modifier on relevance means large, chaotic encounters pull in more bystanders over time. Calm, surgical encounters stay contained. This creates organic escalation without scripting it.
- **Player influence lives here.** The player's mechanism for influencing entity behavior could manifest as direct modifications to participation weights (e.g., "focus on this encounter"), temporary behavior profile overrides (e.g., "hold position"), or item grants that change what actions are available. None of these require modifying the core control loop.
