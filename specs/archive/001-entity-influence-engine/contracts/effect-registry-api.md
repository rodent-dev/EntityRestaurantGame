# Contract: EffectRegistry API

The `EffectRegistry` is the binding layer between JSON effect keywords and runtime behavior.

---

## Interface

```csharp
// Register a new effect type. Call during startup before any items are loaded.
EffectRegistry.Register(string keyword, IActionEffect effect)

// Resolve a keyword to its implementation. Throws if not registered.
EffectRegistry.Resolve(string keyword) → IActionEffect

// Check without throwing.
EffectRegistry.IsRegistered(string keyword) → bool
```

---

## IActionEffect Contract

```csharp
public interface IActionEffect
{
    void Apply(Entity user, Entity target, Dictionary<string, float> parameters);
}
```

Implementations MUST:
- Be stateless (safe to register a single shared instance per keyword).
- Read all configuration from `parameters` — never from fields.
- Not throw on missing optional parameters; use `parameters.GetValueOrDefault(key, defaultValue)`.

---

## Startup Registration Order

`EffectRegistry` MUST be populated before `ItemFactory` is called.
Recommended in `Program.cs` or a `Startup` class:

```csharp
EffectRegistry.Register("Damage",       new DamageEffect());
EffectRegistry.Register("Heal",         new HealEffect());
EffectRegistry.Register("Relationship", new RelationshipEffect());
EffectRegistry.Register("ConsumeItem",  new ConsumeItemEffect());
EffectRegistry.Register("StatModify",   new StatModifyEffect());
```

---

## Extending the Registry

To add a new effect type:
1. Implement `IActionEffect`.
2. Register it with a new keyword string at startup.
3. Use that keyword in any item JSON file — no other changes required.
