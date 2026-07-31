# Module 02 — Behavior Weighting Engine
**Parent:** [Overview](./00_overview.md)
**Status:** Draft v0.1

---

## Responsibility

The Behavior Weighting Engine is the decision-making core of the simulation. It does not pick the *perfect* action for an entity — it builds a probability distribution across all viable actions, then draws from it. This makes entities feel consistent with their personality while remaining unpredictable enough to be interesting.

---

## Core Concept: Weighted Candidate Selection

At any decision point, an entity has a set of available actions (from its `ActionRegistry`). The engine assigns each candidate action a **composite weight** by summing three categories of influence:

```
CompositeWeight(action) =
    BehaviorScore(action)         // From entity's BehaviorProfile dispositions
  + EnvironmentScore(action)      // From current world/scene conditions
  + RelationshipScore(action)     // From affinity toward the action's target
```

The action with the highest composite weight wins — with a configurable jitter applied to prevent pure determinism.

---

## Weight Sources

### 1. Behavior Score

An entity's `BehaviorProfile` contains a list of `BehaviorDispositions`, each with a `BehaviorTag` and a base weight. Actions are also tagged with one or more `BehaviorTags`. The behavior score sums all matching disposition weights.

```csharp
public float ComputeBehaviorScore(ActionDefinition action, BehaviorProfile profile)
{
    float score = 0f;
    foreach (var disposition in profile.Dispositions)
    {
        if (action.BehaviorTags.Contains(disposition.Tag))
            score += disposition.Weight;
    }
    return score;
}
```

**Example:** An entity has `Aggressive(+3.0)` and `Cautious(-1.0)` dispositions. An attack action tagged `[Aggressive]` scores `+3.0`. A retreat action tagged `[Cautious]` scores `-1.0` from that entity, making it actively disfavored by this specific personality.

---

### 2. Environment Score

Environment modifiers are contextual conditions attached to the current scene or encounter. They are evaluated per-action at decision time. These are defined externally and injected into the engine — they are not hardcoded.

```csharp
public interface IEnvironmentModifier
{
    // Returns an additive score delta for this action given the current context
    float Evaluate(ActionDefinition action, Entity actor, EncounterContext context);
}
```

**Example modifiers:**

| Modifier | Condition | Effect |
|----------|-----------|--------|
| `OutnumberedModifier` | Actor's allies < enemies | Boosts `Cautious`-tagged actions |
| `LowHealthModifier` | Actor health < 25% | Boosts retreat/heal, penalizes attack |
| `AloneModifier` | No allies in range | Reduces `Social`-tagged actions (no audience) |
| `CoverAvailableModifier` | Cover tile adjacent | Boosts defensive repositioning actions |

Environment modifiers are registered to the engine at scene setup. They are not global — they are contextual per encounter.

---

### 3. Relationship Score

If an action targets a specific entity, the actor's affinity toward that entity contributes an additive modifier.

```csharp
public float ComputeRelationshipScore(ActionDefinition action, Entity actor)
{
    if (action.Target == null) return 0f;

    float affinity = actor.Relationships.GetAffinity(action.Target.Id);
    // Affinity is [-1, 1]. Scale to meaningful weight range.
    return affinity * RelationshipWeightScale; // e.g., scale = 2.0f
}
```

A strong positive affinity (`+1.0`) toward a target boosts helpful or social actions aimed at them. A strong negative affinity boosts hostile actions and suppresses cooperative ones.

---

## Composite Scoring & Selection

```csharp
public ActionDefinition SelectAction(
    Entity actor,
    IReadOnlyList<ActionDefinition> candidates,
    EncounterContext context,
    IReadOnlyList<IEnvironmentModifier> modifiers,
    float jitterMagnitude = 0.5f)
{
    if (candidates.Count == 0) return null;

    var scored = new List<(ActionDefinition action, float weight)>();

    foreach (var action in candidates)
    {
        float weight = 0f;
        weight += ComputeBehaviorScore(action, actor.BehaviorProfile);
        weight += ComputeRelationshipScore(action, actor);
        foreach (var mod in modifiers)
            weight += mod.Evaluate(action, actor, context);

        // Apply jitter — small random perturbation, not a full reroll
        weight += (_rng.NextSingle() * 2f - 1f) * jitterMagnitude;

        // Negative weights are allowed — floor prevents complete impossibility from math artifacts
        weight = Math.Max(weight, WeightFloor); // WeightFloor = 0.01f

        scored.Add((action, weight));
    }

    // Weighted random draw (not just argmax) — preserves probability texture
    return WeightedDraw(scored);
}
```

### WeightedDraw vs. ArgMax

Using a **weighted random draw** rather than strict argmax is intentional. Even if an attack action has a weight of `8.0` and a talk action has `1.0`, the entity will occasionally talk. This models the reality that even aggressive characters sometimes try diplomacy — just rarely. The ratio of their weights reflects their relative likelihood, not a hard threshold.

```csharp
private ActionDefinition WeightedDraw(List<(ActionDefinition action, float weight)> scored)
{
    float total = scored.Sum(s => s.weight);
    float roll = _rng.NextSingle() * total;
    float cumulative = 0f;

    foreach (var (action, weight) in scored)
    {
        cumulative += weight;
        if (roll <= cumulative) return action;
    }

    return scored[^1].action; // Fallback: last element
}
```

---

## Jitter System

Jitter is a small, bounded random perturbation added to each candidate's weight before selection. Its purpose is **texture, not chaos** — it prevents the simulation from being trivially predictable (same inputs → exact same output every time) without destabilizing the behavioral tendencies.

```csharp
// Jitter magnitude is configurable per encounter or scene
public class JitterSettings
{
    public float Magnitude { get; set; } = 0.5f;  // Max perturbation ± this value
    public float Seed { get; set; }                // Optional: deterministic replay support
}
```

**Design guidance:**
- Low jitter (0.1–0.5): Entities feel consistent, only occasionally surprising.
- Medium jitter (0.5–1.5): Entities feel human — reliable patterns with occasional deviation.
- High jitter (2.0+): Entities feel erratic. Reserve for specific archetypes (e.g., an unstable character).

The player-facing difficulty setting could directly modulate jitter — higher difficulty = lower jitter = more optimally dangerous enemies.

---

## BehaviorTag Reference

Tags are shared between `BehaviorDisposition` entries and `ActionDefinition` entries. They are the connective tissue between personality and behavior.

| Tag | Typical Entity Dispositions | Typical Action Types |
|-----|-----------------------------|----------------------|
| `Aggressive` | Fighters, berserkers | Attack, charge, intimidate |
| `Cautious` | Tacticians, veterans | Retreat, seek cover, delay |
| `Social` | Leaders, diplomats | Speak, negotiate, rally |
| `Protective` | Guards, parents | Defend ally, intercept |
| `Cowardly` | Civilians, minions | Flee, hide, surrender |
| `Opportunistic` | Thieves, scavengers | Loot, steal, ambush |
| `Curious` | Scholars, explorers | Investigate, approach unknown |

This list is not exhaustive — new tags can be introduced by adding them to the shared `BehaviorTag` enum.

---

## Design Notes

- **Weights are additive, not multiplicative.** This keeps the math transparent and debuggable. You can read a weight total and reason about exactly where it came from.
- **There is no hidden global personality.** All behavior emerges from the three explicit sources above. This makes tuning predictable.
- **The engine is stateless per invocation.** It does not remember past decisions. Consistency in behavior comes from the stability of the entity's profile, not from engine state.
- **Negative weights are valid and meaningful.** A disposition of `Cowardly(-2.0)` on an aggressive entity is a meaningful suppression of retreat actions, not a bug.
