# Contract: Action System API

---

## IAction

```csharp
public interface IAction
{
    string ActionName { get; }

    /// When true, Execute throws ArgumentNullException if item is null.
    /// When false, Execute runs without an item; an item present acts as an optional enhancer.
    bool RequiresItem { get; }

    List<IItem> GetViableItems(Entity user, Entity target);
    void Execute(Entity user, Entity target, IItem? item);
    bool CanActorUse(Entity user);
    bool CanTarget(Entity user, Entity target);
}
```

---

## RequiresItem contract

`RequiresItem` controls two separate behaviours:

### In `EntityController.ProcessTick`
```
items  = action.GetViableItems(user, target)
item   = items.FirstOrDefault()

if item == null AND action.RequiresItem  → skip (no suitable item in inventory)
if item == null AND !action.RequiresItem → proceed with item = null
```

### In `Execute`
| `RequiresItem` | Item present | Item null | Expected behaviour |
|---|---|---|---|
| `true` | Proceed normally | `ArgumentNullException.ThrowIfNull(item)` | Item is mandatory |
| `false` | Use item as enhancer | Run base logic without item | Item is optional |

Actions that use `RequiresItem = false` should never assume `item != null`.
Actions that use `RequiresItem = true` may call `item!.Apply(...)` safely after the guard.

---

## Skill items and optional enhancement

Actions with `RequiresItem = false` are designed to work with or without inventory items.
A `Skill`-type item in the entity's inventory can be registered for the same action name,
providing an enhancement effect (e.g. better loot, reduced cost) without being consumed.

```
Scavenge (RequiresItem = false)
  └─ No item           → base success rate
  └─ digging_skill     → ScavengeAction reads the item's effect parameters to amplify outcome
  └─ shovel (Weapon)   → same path; item type doesn't matter, only enablesActions does
```

`GetViableItems` for optional-item actions returns enhancers if present. The action's
`CanActorUse` returns `true` regardless.

---

## Implemented actions

| Action | RequiresItem | Target | Typical enabling items |
|--------|-------------|--------|------------------------|
| `Attack` | `true` | Others | Weapons (fist, knife, rolling_pin) |
| `Defend` | `true` | Self | Weapons / shields |
| `Speak` | `true` | Others | Merit items (bouquet, business_card, wine_bottle) |
| `Consume` | `true` | Self | Consumables (bread_loaf, steak, coffee_cup) |
| `Enchant` | `true` | Others | Standard/Merit (blessing_herb, hex_token) |
| `Grapple` | `true` | Others | Weapons (fist) |
| `Scavenge` *(planned)* | `false` | Self / Zone | Skills (digging_skill), Tools (shovel) |

---

## Adding a new action

1. Create `EntityObjects/Actions/YourAction.cs` implementing `IAction`.
2. Set `RequiresItem` to `true` if an item is always needed, `false` if it is optional.
3. In `Execute`, guard with `ArgumentNullException.ThrowIfNull(item)` if `RequiresItem = true`.
4. Attach via `entity.GetActionModule().AttachAction(new YourAction())` in `Program.cs`.
5. Add a `BehaviorModule` with `ActionType = "YourActionName"` to any entity that should favour it.
6. Create or update item JSONs with `"enablesActions": ["YourActionName"]` to enable it.
